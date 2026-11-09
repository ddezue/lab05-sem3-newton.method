using System;
using System.Collections.Generic;

namespace NewtonMethod
{
  public enum OptimizationMode { Minimum, Maximum, Root }

  public class NewtonStep
  {
    public double X;
    public double Y;
  }

  public class NewtonResult
  {
    public List<NewtonStep> Steps = new List<NewtonStep>();
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
    private const int ScanSteps = 50; // Количество стартовых точек по всему отрезку

    public static NewtonResult Solve(
        Func<double, double> function,
        double a, double b, double eps,
        OptimizationMode mode)
    {
      var result = new NewtonResult { Mode = mode };

      if (function == null) { result.Error = "Функция не задана"; return result; }
      if (a >= b) { result.Error = "Требуется a < b"; return result; }
      if (eps <= 0) { result.Error = "Точность e должна быть > 0"; return result; }

      double globalBestX = double.NaN;
      double globalBestY = mode == OptimizationMode.Minimum ? double.MaxValue : double.MinValue;
      List<NewtonStep> bestSteps = null;

      // 1. Проверяем САМИ ГРАНИЦЫ (глобальный экстремум может быть на краю)
      CheckBound(function, a, mode, ref globalBestX, ref globalBestY, ref bestSteps);
      CheckBound(function, b, mode, ref globalBestX, ref globalBestY, ref bestSteps);

      // 2. Запускаем метод Ньютона из 50 точек по всему отрезку
      for (int i = 1; i < ScanSteps; i++)
      {
        double startX = a + (b - a) * i / ScanSteps;
        var steps = FindLocalExtremum(function, startX, eps);

        if (steps != null && steps.Count > 0)
        {
          double localX = steps[steps.Count - 1].X;
          double localY = steps[steps.Count - 1].Y;

          // Если этот локальный экстремум оказался лучше глобального - запоминаем его
          bool isBetter = (mode == OptimizationMode.Minimum && localY < globalBestY) ||
                          (mode == OptimizationMode.Maximum && localY > globalBestY);

          if (isBetter)
          {
            globalBestX = localX;
            globalBestY = localY;
            bestSteps = steps; // Запоминаем маршрут именно этого "победителя"
          }
        }
      }

      if (double.IsNaN(globalBestX))
      {
        result.Error = "Не удалось найти экстремум. Возможно, производная нигде не равна нулю.";
        return result;
      }

      // Если лучший результат оказался на границе, выводим предупреждение
      if (Math.Abs(globalBestX - a) < eps || Math.Abs(globalBestX - b) < eps)
      {
        string kind = mode == OptimizationMode.Minimum ? "минимум" : "максимум";
        result.Warning = $"Глобальный {kind} находится на границе интервала [a; b].";
      }

      result.Point = globalBestX;
      result.Value = globalBestY;
      result.Steps = bestSteps; // Отдаем историю шагов в интерфейс
      result.Success = true;
      return result;
    }

    // Выполняет сам метод Ньютона из конкретной точки
    private static List<NewtonStep> FindLocalExtremum(Func<double, double> f, double startX, double eps)
    {
      var steps = new List<NewtonStep>();
      double x = startX;

      double startY;
      if (!TryEvaluate(f, x, out startY)) return null;

      steps.Add(new NewtonStep { X = x, Y = startY });

      for (int i = 0; i < MaxIterations; i++)
      {
        double dfx = Derivative(f, x);
        double ddfx = SecondDerivative(f, x);

        // Проверки на разрывы и деление на ноль
        if (double.IsNaN(dfx) || double.IsInfinity(dfx) ||
            double.IsNaN(ddfx) || double.IsInfinity(ddfx) ||
            Math.Abs(ddfx) < 1e-10)
        {
          return null;
        }

        // Шаг Ньютона
        double nextX = x - dfx / ddfx;

        if (double.IsNaN(nextX) || double.IsInfinity(nextX)) return null;

        // Записываем шаг, если точка реально сдвинулась
        if (Math.Abs(nextX - x) > 1e-12)
        {
          double nextY;
          if (TryEvaluate(f, nextX, out nextY))
          {
            steps.Add(new NewtonStep { X = nextX, Y = nextY });
          }
          else return null;
        }

        // Проверка точности (сошлись или нет)
        if (Math.Abs(nextX - x) < eps)
        {
          return steps;
        }

        x = nextX;
      }
      return null; // Не сошлось за 100 итераций
    }

    // Проверка границ интервала
    private static void CheckBound(Func<double, double> f, double x, OptimizationMode mode, ref double bestX, ref double bestY, ref List<NewtonStep> bestSteps)
    {
      double y;
      if (!TryEvaluate(f, x, out y)) return;
      if (Math.Abs(y) > 1e5) return; // Отсеиваем полюса

      bool isBetter = (mode == OptimizationMode.Minimum && y < bestY) ||
                      (mode == OptimizationMode.Maximum && y > bestY);

      if (isBetter || double.IsNaN(bestX))
      {
        bestX = x;
        bestY = y;
        bestSteps = new List<NewtonStep> { new NewtonStep { X = x, Y = y } }; // На границе всего 1 шаг
      }
    }

    // 1-я производная (численно)
    private static double Derivative(Func<double, double> f, double x)
    {
      double h = 1e-6 * Math.Max(1.0, Math.Abs(x));
      double fPlus, fMinus;
      if (!TryEvaluate(f, x + h, out fPlus) || !TryEvaluate(f, x - h, out fMinus)) return double.NaN;
      return (fPlus - fMinus) / (2.0 * h);
    }

    // 2-я производная (численно)
    private static double SecondDerivative(Func<double, double> f, double x)
    {
      double h = 1e-4 * Math.Max(1.0, Math.Abs(x));
      double f0, fPlus, fMinus;
      if (!TryEvaluate(f, x, out f0) || !TryEvaluate(f, x + h, out fPlus) || !TryEvaluate(f, x - h, out fMinus)) return double.NaN;
      return (fPlus - 2.0 * f0 + fMinus) / (h * h);
    }

    private static bool TryEvaluate(Func<double, double> f, double x, out double y)
    {
      try
      {
        y = f(x);
        return !double.IsNaN(y) && !double.IsInfinity(y);
      }
      catch
      {
        y = double.NaN;
        return false;
      }
    }
  }
}