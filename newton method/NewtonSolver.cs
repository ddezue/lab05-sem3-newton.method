using System;

namespace NewtonMethod
{
  public enum OptimizationMode
  {
    Minimum,
    Maximum,
    Root
  }

  public class NewtonResult
  {
    public double Point;
    public double Value;
    public bool Success;
    public string Error = string.Empty;
    public string Warning = string.Empty;
    public OptimizationMode Mode;
  }

  public static class NewtonSolver
  {
    private const int MaxIterations = 100;
    private const int ScanSteps = 50; // Количество стартовых точек для поиска глобального экстремума

    public static NewtonResult Solve(
        Func<double, double> function,
        double a, double b, double eps,
        OptimizationMode mode)
    {
      var result = new NewtonResult { Mode = mode };

      // Проверка входных данных
      if (function == null)
      {
        result.Error = "Функция не задана";
        return result;
      }

      if (!IsFinite(a) || !IsFinite(b) || !IsFinite(eps))
      {
        result.Error = "Входные значения должны быть конечными числами";
        return result;
      }

      if (a >= b)
      {
        result.Error = "Требуется a < b";
        return result;
      }

      if (eps <= 0)
      {
        result.Error = "Точность e должна быть > 0";
        return result;
      }

      if (eps >= (b - a))
      {
        result.Error = "Точность e больше длины интервала";
        return result;
      }

      if (mode == OptimizationMode.Root)
      {
        result.Error = "В этой программе предусмотрен поиск глобального минимума или максимума";
        return result;
      }

      // 1. Проверка вычислимости функции на границах интервала
      if (!TryEvaluate(function, a, out _) || !TryEvaluate(function, b, out _))
      {
        result.Error = "Функция не вычислима на границах интервала";
        return result;
      }

      // 2. Поиск экстремумов (корней производной) мультистартом
      double globalBestX = double.NaN;
      double globalBestY = mode == OptimizationMode.Minimum ? double.MaxValue : double.MinValue;

      // Проверяем границы: глобальный экстремум может находиться на краю отрезка
      CheckPoint(function, a, mode, eps, ref globalBestX, ref globalBestY);
      CheckPoint(function, b, mode, eps, ref globalBestX, ref globalBestY);

      // Запускаем метод Ньютона из нескольких начальных точек внутри отрезка
      for (int i = 1; i < ScanSteps; i++)
      {
        double startX = a + (b - a) * i / ScanSteps;
        double rootX = FindExtremumByNewton(function, startX, eps);

        // Учитываем только стационарные точки внутри заданного отрезка
        if (IsFinite(rootX) && rootX > a && rootX < b)
        {
          CheckPoint(function, rootX, mode, eps, ref globalBestX, ref globalBestY);
        }
      }

      if (!IsFinite(globalBestX))
      {
        result.Error = "Не удалось найти экстремум. Возможно, производная нигде не равна нулю.";
        return result;
      }

      // Проверяем, совпадает ли найденная точка с границей
      if (Math.Abs(globalBestX - a) < eps || Math.Abs(globalBestX - b) < eps)
      {
        string kind = mode == OptimizationMode.Minimum ? "минимум" : "максимум";
        result.Warning = $"Глобальный {kind} находится на границе интервала [a; b].";
      }

      result.Point = globalBestX;
      result.Value = globalBestY;
      result.Success = true;
      return result;
    }

    // Локальный метод Ньютона для поиска корня f'(x) = 0
    private static double FindExtremumByNewton(Func<double, double> f, double startX, double eps)
    {
      double x = startX;

      for (int i = 0; i < MaxIterations; i++)
      {
        double dfx = Derivative(f, x);
        double ddfx = SecondDerivative(f, x);

        if (!IsFinite(dfx) || !IsFinite(ddfx))
          return double.NaN;

        // Если вторая производная около нуля, метод Ньютона не работает
        if (Math.Abs(ddfx) < 1e-10)
          return double.NaN;

        double nextX = x - dfx / ddfx;

        // Защита от переполнения и ухода в бесконечность
        if (!IsFinite(nextX) || Math.Abs(nextX) > 1e7)
          return double.NaN;

        if (Math.Abs(nextX - x) < eps)
          return nextX;

        x = nextX;
      }

      return double.NaN; // Метод не сошёлся за MaxIterations итераций
    }

    private static void CheckPoint(
        Func<double, double> f,
        double x,
        OptimizationMode mode,
        double eps,
        ref double bestX,
        ref double bestY)
    {
      if (!TryEvaluate(f, x, out double y))
        return;

      // Отсеиваем слишком большие значения, которые могут соответствовать полюсам
      if (Math.Abs(y) > 1e5)
        return;

      bool isBetter =
          (mode == OptimizationMode.Minimum && y < bestY) ||
          (mode == OptimizationMode.Maximum && y > bestY);

      if (isBetter)
      {
        bestX = x;
        bestY = y;
      }
    }

    // Первая производная (центральная разность)
    private static double Derivative(Func<double, double> f, double x)
    {
      double h = 1e-6 * Math.Max(1.0, Math.Abs(x));

      if (!TryEvaluate(f, x + h, out double fPlus) ||
          !TryEvaluate(f, x - h, out double fMinus))
        return double.NaN;

      return (fPlus - fMinus) / (2.0 * h);
    }

    // Вторая производная (центральная разность)
    private static double SecondDerivative(Func<double, double> f, double x)
    {
      double h = 1e-4 * Math.Max(1.0, Math.Abs(x));

      if (!TryEvaluate(f, x, out double f0) ||
          !TryEvaluate(f, x + h, out double fPlus) ||
          !TryEvaluate(f, x - h, out double fMinus))
        return double.NaN;

      return (fPlus - 2.0 * f0 + fMinus) / (h * h);
    }

    private static bool TryEvaluate(Func<double, double> f, double x, out double value)
    {
      try
      {
        value = f(x);
        return IsFinite(value);
      }
      catch
      {
        value = double.NaN;
        return false;
      }
    }

    private static bool IsFinite(double value)
    {
      return !double.IsNaN(value) && !double.IsInfinity(value);
    }
  }
}
