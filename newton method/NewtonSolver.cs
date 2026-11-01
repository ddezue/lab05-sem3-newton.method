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
    public string Error;
    public string Warning;
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

      if (double.IsNaN(a) || double.IsNaN(b) || double.IsNaN(eps))
      { result.Error = "Входные значения не числа"; return result; }

      if (a >= b) { result.Error = "Требуется a < b"; return result; }
      if (eps <= 0) { result.Error = "Точность e должна быть > 0"; return result; }
      if (eps >= (b - a)) { result.Error = "Точность e больше длины интервала"; return result; }

      // 1. Проверка на разрывы и вычислимость
      if (!TryEvaluate(function, a, out _) || !TryEvaluate(function, b, out _))
      {
        result.Error = "Функция не вычислима на границах интервала";
        return result;
      }

      // 2. Поиск экстремумов (корней производной) мультистартом
      double globalBestX = double.NaN;
      double globalBestY = mode == OptimizationMode.Minimum ? double.MaxValue : double.MinValue;

      // Проверяем границы (глобальный экстремум может быть на краю)
      CheckPoint(function, a, mode, eps, ref globalBestX, ref globalBestY);
      CheckPoint(function, b, mode, eps, ref globalBestX, ref globalBestY);

      // Сканируем внутренность отрезка
      for (int i = 1; i < ScanSteps; i++)
      {
        double startX = a + (b - a) * i / ScanSteps;
        double rootX = FindExtremumByNewton(function, startX, eps);

        if (!double.IsNaN(rootX) && rootX > a && rootX < b)
        {
          CheckPoint(function, rootX, mode, eps, ref globalBestX, ref globalBestY);
        }
      }

      if (double.IsNaN(globalBestX))
      {
        result.Error = "Не удалось найти экстремум. Возможно, производная нигде не равна нулю.";
        return result;
      }

      // Проверяем, совпадает ли найденная точка с границей
      if (Math.Abs(globalBestX - a) < eps || Math.Abs(globalBestX - b) < eps)
      {
        string kind = (mode == OptimizationMode.Minimum) ? "минимум" : "максимум";
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

        if (double.IsNaN(dfx) || double.IsNaN(ddfx) || double.IsInfinity(dfx) || double.IsInfinity(ddfx))
          return double.NaN;

        // Если вторая производная около нуля, метод Ньютона не работает (деление на ноль)
        if (Math.Abs(ddfx) < 1e-10)
          return double.NaN;

        double nextX = x - dfx / ddfx;

        // Защита от улетания в бесконечность
        if (Math.Abs(nextX) > 1e7) return double.NaN;

        if (Math.Abs(nextX - x) < eps)
          return nextX;

        x = nextX;
      }
      return double.NaN; // Не сошлось
    }

    private static void CheckPoint(Func<double, double> f, double x, OptimizationMode mode, double eps, ref double bestX, ref double bestY)
    {
      double y;
      if (!TryEvaluate(f, x, out y)) return;

      // Отсеиваем полюса (например, у tan(x))
      if (Math.Abs(y) > 1e5) return;

      bool isBetter = false;
      if (mode == OptimizationMode.Minimum && y < bestY) isBetter = true;
      if (mode == OptimizationMode.Maximum && y > bestY) isBetter = true;

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
      if (!TryEvaluate(f, x + h, out double fPlus)) return double.NaN;
      if (!TryEvaluate(f, x - h, out double fMinus)) return double.NaN;
      return (fPlus - fMinus) / (2.0 * h);
    }

    // Вторая производная (центральная разность)
    private static double SecondDerivative(Func<double, double> f, double x)
    {
      double h = 1e-4 * Math.Max(1.0, Math.Abs(x));
      if (!TryEvaluate(f, x, out double f0)) return double.NaN;
      if (!TryEvaluate(f, x + h, out double fPlus)) return double.NaN;
      if (!TryEvaluate(f, x - h, out double fMinus)) return double.NaN;
      return (fPlus - 2 * f0 + fMinus) / (h * h);
    }

    private static bool TryEvaluate(Func<double, double> f, double x, out double v)
    {
      try
      {
        v = f(x);
        return !double.IsNaN(v) && !double.IsInfinity(v);
      }
      catch
      {
        v = double.NaN;
        return false;
      }
    }
  }
}