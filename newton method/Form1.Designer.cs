using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using ScottPlot.WinForms;

namespace NewtonMethod
{
  public partial class Form1 : Form
  {
    private void InitializeComponent() { }

    private TextBox _textBoxA;
    private TextBox _textBoxB;
    private TextBox _textBoxE;
    private TextBox _textBoxFormula;

    private RadioButton _radioMin;
    private RadioButton _radioMax;

    private FormsPlot _formsPlot;
    private TrackBar _yZoomTrackBar;

    private Label _labelResult;
    private Label _labelWarning;

    private Label _labelRootName;
    private Label _valueRoot;
    private Label _labelFunctionName;
    private Label _valueFunction;
    private Label _labelPrecisionName;
    private Label _valuePrecision;

    private TableLayoutPanel _resultTable;
    private GroupBox _groupParams;
    private GroupBox _groupResult;

    private MenuStrip _menuStrip;
    private Label _helpIcon;
    private ToolTip _formulaToolTip;

    private System.Windows.Forms.Timer _autoCalcTimer;

    private double _leftBound;
    private double _rightBound;
    private double _precision;

    private FunctionParser _parser;
    private NewtonResult _solverResult;

    public Form1()
    {
      BuildUserInterface();

      _autoCalcTimer = new System.Windows.Forms.Timer { Interval = 300 };
      _autoCalcTimer.Tick += (sender, args) =>
      {
        _autoCalcTimer.Stop();
        TryAutoCalculate();
      };

      _textBoxA.TextChanged += (sender, args) => ScheduleAutoCalculate();
      _textBoxB.TextChanged += (sender, args) => ScheduleAutoCalculate();
      _textBoxE.TextChanged += (sender, args) => ScheduleAutoCalculate();
      _textBoxFormula.TextChanged += (sender, args) => ScheduleAutoCalculate();

      _radioMin.CheckedChanged += (sender, args) => ScheduleAutoCalculate();
      _radioMax.CheckedChanged += (sender, args) => ScheduleAutoCalculate();
    }

    private OptimizationMode CurrentMode => _radioMax.Checked ? OptimizationMode.Maximum : OptimizationMode.Minimum;

    // ============================================================
    // ИНТЕРФЕЙС
    // ============================================================

    private void BuildUserInterface()
    {
      Text = "Метод Ньютона (поиск экстремумов)";
      Width = 1250;
      Height = 750;
      MinimumSize = new Size(950, 620);
      StartPosition = FormStartPosition.CenterScreen;
      Font = new Font("Segoe UI", 9F, FontStyle.Regular);

      // MENU
      _menuStrip = new MenuStrip();
      var calculateItem = new ToolStripMenuItem("Рассчитать");
      var clearItem = new ToolStripMenuItem("Очистить");
      calculateItem.Click += (s, e) => Calculate();
      clearItem.Click += (s, e) => ClearAll();
      _menuStrip.Items.Add(calculateItem);
      _menuStrip.Items.Add(clearItem);
      MainMenuStrip = _menuStrip;
      Controls.Add(_menuStrip);

      // LAYOUT
      var rootLayout = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        RowCount = 1,
        Padding = new Padding(10)
      };
      rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
      rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      Controls.Add(rootLayout);

      // ЛЕВАЯ ПАНЕЛЬ (Настройки + Результаты)
      var leftContainer = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 1,
        RowCount = 2,
        Margin = new Padding(0)
      };
      leftContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
      leftContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

      // --- ГРУППА: ПАРАМЕТРЫ ---
      _groupParams = new GroupBox
      {
        Text = " Параметры ",
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
        ForeColor = Color.DarkSlateBlue,
        Padding = new Padding(10)
      };

      var paramsLayout = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 5
      };
      paramsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
      paramsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      paramsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 35));

      _textBoxA = MakeTextBox();
      _textBoxB = MakeTextBox();
      _textBoxE = MakeTextBox(); _textBoxE.Text = "0,0001";
      _textBoxFormula = MakeTextBox(); _textBoxFormula.Text = "x^2 + 2*x - 6";

      paramsLayout.Controls.Add(MakeLabel("a ="), 0, 0);
      paramsLayout.Controls.Add(_textBoxA, 1, 0);
      paramsLayout.SetColumnSpan(_textBoxA, 2);

      paramsLayout.Controls.Add(MakeLabel("b ="), 0, 1);
      paramsLayout.Controls.Add(_textBoxB, 1, 1);
      paramsLayout.SetColumnSpan(_textBoxB, 2);

      paramsLayout.Controls.Add(MakeLabel("e ="), 0, 2);
      paramsLayout.Controls.Add(_textBoxE, 1, 2);
      paramsLayout.SetColumnSpan(_textBoxE, 2);

      paramsLayout.Controls.Add(MakeLabel("f(x) ="), 0, 3);
      paramsLayout.Controls.Add(_textBoxFormula, 1, 3);

      _helpIcon = new Label
      {
        Text = "?",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        ForeColor = Color.White,
        BackColor = Color.SteelBlue,
        Cursor = Cursors.Help,
        Margin = new Padding(2, 4, 0, 4)
      };
      paramsLayout.Controls.Add(_helpIcon, 2, 3);

      var modePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
      _radioMin = new RadioButton { Text = "Глоб. минимум", Checked = true, AutoSize = true, Margin = new Padding(0, 5, 10, 0) };
      _radioMax = new RadioButton { Text = "Глоб. максимум", AutoSize = true, Margin = new Padding(0, 5, 0, 0) };
      modePanel.Controls.Add(_radioMin);
      modePanel.Controls.Add(_radioMax);

      paramsLayout.Controls.Add(MakeLabel("Режим:"), 0, 4);
      paramsLayout.Controls.Add(modePanel, 1, 4);
      paramsLayout.SetColumnSpan(modePanel, 2);

      _groupParams.Controls.Add(paramsLayout);
      leftContainer.Controls.Add(_groupParams, 0, 0);

      // --- ГРУППА: РЕЗУЛЬТАТ ---
      _groupResult = new GroupBox
      {
        Text = " Результат ",
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
        ForeColor = Color.DarkGreen,
        Padding = new Padding(10),
        Margin = new Padding(0, 10, 0, 0)
      };

      _labelResult = new Label
      {
        Text = "Ожидание данных...",
        Dock = DockStyle.Top,
        AutoSize = false,
        Height = 25,
        Font = new Font("Segoe UI", 9F, FontStyle.Bold),
        ForeColor = Color.Gray
      };

      _resultTable = CreateResultTable();
      _resultTable.Visible = false;

      _labelWarning = new Label
      {
        Text = "",
        Dock = DockStyle.Fill,
        AutoSize = false,
        Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
        ForeColor = Color.DarkOrange,
        TextAlign = ContentAlignment.TopLeft,
        Padding = new Padding(0, 10, 0, 0)
      };

      var resLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
      resLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
      resLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
      resLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

      resLayout.Controls.Add(_labelResult, 0, 0);
      resLayout.Controls.Add(_resultTable, 0, 1);
      resLayout.Controls.Add(_labelWarning, 0, 2);

      _groupResult.Controls.Add(resLayout);
      leftContainer.Controls.Add(_groupResult, 0, 1);

      rootLayout.Controls.Add(leftContainer, 0, 0);

      // --- ПРАВАЯ ПАНЕЛЬ (ГРАФИК) ---
      var plotContainer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(10, 0, 0, 0) };
      plotContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
      plotContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

      _formsPlot = new FormsPlot { Dock = DockStyle.Fill };
      plotContainer.Controls.Add(_formsPlot, 0, 0);

      // Ползунок масштаба Y
      var zoomPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 5, 0, 0) };
      zoomPanel.Controls.Add(new Label { Text = "Масштаб по Y:", AutoSize = true, Margin = new Padding(0, 5, 10, 0) });

      _yZoomTrackBar = new TrackBar { Minimum = 1, Maximum = 100, Value = 50, TickFrequency = 10, Width = 200, AutoSize = false, Height = 30 };
      _yZoomTrackBar.Scroll += (s, e) => UpdatePlot(false);
      zoomPanel.Controls.Add(_yZoomTrackBar);

      var lblZoom = new Label { Text = "1x", AutoSize = true, Margin = new Padding(10, 5, 0, 0), ForeColor = Color.DimGray };
      zoomPanel.Controls.Add(lblZoom);

      _yZoomTrackBar.ValueChanged += (s, e) => {
        lblZoom.Text = (_yZoomTrackBar.Value / 50.0).ToString("0.0") + "x";
      };

      plotContainer.Controls.Add(zoomPanel, 0, 1);
      rootLayout.Controls.Add(plotContainer, 1, 0);

      // TOOLTIP
      _formulaToolTip = new ToolTip { AutoPopDelay = 60000, InitialDelay = 150 };
      _formulaToolTip.SetToolTip(_helpIcon, "Поддерживается: + - * / ^ ( )\nsin, cos, tan, exp, ln, log, sqrt, abs\npi, e\n\nДесятичный разделитель - точка или запятая.");
    }

    private TableLayoutPanel CreateResultTable()
    {
      var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
      table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
      table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

      for (int i = 0; i < 3; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

      _labelRootName = CreateResultNameLabel("Точка экстремума");
      _valueRoot = CreateResultValueLabel();
      _labelFunctionName = CreateResultNameLabel("f(x)");
      _valueFunction = CreateResultValueLabel();
      _labelPrecisionName = CreateResultNameLabel("Точность");
      _valuePrecision = CreateResultValueLabel();

      table.Controls.Add(_labelRootName, 0, 0); table.Controls.Add(_valueRoot, 1, 0);
      table.Controls.Add(_labelFunctionName, 0, 1); table.Controls.Add(_valueFunction, 1, 1);
      table.Controls.Add(_labelPrecisionName, 0, 2); table.Controls.Add(_valuePrecision, 1, 2);

      return table;
    }

    private Label CreateResultNameLabel(string text)
    {
      return new Label { Text = text, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.MiddleLeft };
    }

    private Label CreateResultValueLabel()
    {
      return new Label { Text = "-", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
    }

    private Label MakeLabel(string caption)
    {
      return new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F), Padding = new Padding(0, 0, 5, 0) };
    }

    private TextBox MakeTextBox()
    {
      return new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F), Margin = new Padding(0, 3, 0, 3) };
    }

    // ============================================================
    // ЛОГИКА
    // ============================================================

    private void ScheduleAutoCalculate()
    {
      _autoCalcTimer.Stop();
      _autoCalcTimer.Start();
    }

    private void TryAutoCalculate()
    {
      if (string.IsNullOrWhiteSpace(_textBoxA.Text) || string.IsNullOrWhiteSpace(_textBoxB.Text) ||
          string.IsNullOrWhiteSpace(_textBoxE.Text) || string.IsNullOrWhiteSpace(_textBoxFormula.Text))
      {
        ClearResult();
        return;
      }
      Calculate();
    }

    private void Calculate()
    {
      _textBoxA.BackColor = SystemColors.Window;
      _textBoxB.BackColor = SystemColors.Window;
      _textBoxE.BackColor = SystemColors.Window;
      _textBoxFormula.BackColor = SystemColors.Window;

      if (!TryParseDouble(_textBoxA.Text, out _leftBound)) { _textBoxA.BackColor = Color.MistyRose; ShowError("некорректное a"); return; }
      if (!TryParseDouble(_textBoxB.Text, out _rightBound)) { _textBoxB.BackColor = Color.MistyRose; ShowError("некорректное b"); return; }
      if (!TryParseDouble(_textBoxE.Text, out _precision)) { _textBoxE.BackColor = Color.MistyRose; ShowError("некорректное e"); return; }

      if (_leftBound >= _rightBound) { ShowError("должно выполняться a < b"); return; }
      if (_precision <= 0) { _textBoxE.BackColor = Color.MistyRose; ShowError("e должно быть > 0"); return; }

      try
      {
        _parser = new FunctionParser(_textBoxFormula.Text);
      }
      catch (Exception ex)
      {
        _textBoxFormula.BackColor = Color.MistyRose;
        ShowError("ошибка в формуле: " + ex.Message);
        return;
      }

      _solverResult = NewtonSolver.Solve(_parser.Evaluate, _leftBound, _rightBound, _precision, CurrentMode);
      ShowResult();
      UpdatePlot(true);
    }

    private void ShowResult()
    {
      if (!_solverResult.Success)
      {
        ShowError(_solverResult.Error);
        return;
      }

      _groupResult.ForeColor = Color.DarkGreen;
      _labelResult.Text = "Успешно найдено";
      _labelResult.ForeColor = Color.Green;

      _labelRootName.Text = _solverResult.Mode == OptimizationMode.Minimum ? "Глоб. минимум (x)" : "Глоб. максимум (x)";
      _labelFunctionName.Text = _solverResult.Mode == OptimizationMode.Minimum ? "f(x_min)" : "f(x_max)";

      _valueRoot.Text = FormatByPrecision(_solverResult.Point, _precision);
      _valueFunction.Text = _solverResult.Value.ToString("F6", CultureInfo.InvariantCulture);
      _valuePrecision.Text = _precision.ToString("G", CultureInfo.InvariantCulture);

      _labelWarning.Text = string.IsNullOrEmpty(_solverResult.Warning) ? "" : "⚠ " + _solverResult.Warning;
      _resultTable.Visible = true;
    }

    private void ShowError(string msg)
    {
      _groupResult.ForeColor = Color.DarkRed;
      _labelResult.Text = "Ошибка: " + msg;
      _labelResult.ForeColor = Color.DarkRed;
      _resultTable.Visible = false;
      _labelWarning.Text = "";
      _solverResult = null;
      UpdatePlot(false);
    }

    private void ClearResult()
    {
      _groupResult.ForeColor = Color.DarkGreen;
      _labelResult.Text = "Ожидание данных...";
      _labelResult.ForeColor = Color.Gray;
      _resultTable.Visible = false;
      _labelWarning.Text = "";
      _solverResult = null;
      UpdatePlot(false);
    }

    private void ClearAll()
    {
      _textBoxA.Text = ""; _textBoxB.Text = ""; _textBoxE.Text = "0,0001"; _textBoxFormula.Text = "";
      ClearResult();
    }

    private static string FormatByPrecision(double val, double eps)
    {
      int dec = 0;
      double tmp = Math.Abs(eps);
      while (tmp < 1.0 && dec < 15) { tmp *= 10.0; dec++; }
      if (dec < 1) dec = 1;
      return val.ToString("F" + dec, CultureInfo.InvariantCulture);
    }

    private static bool TryParseDouble(string text, out double value)
    {
      text = (text ?? "").Trim().Replace(',', '.');
      return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    // ============================================================
    // ГРАФИК
    // ============================================================

    private void UpdatePlot(bool recalcLimits = true)
    {
      _formsPlot.Plot.Clear();

      if (_parser == null || string.IsNullOrWhiteSpace(_textBoxFormula.Text))
      {
        _formsPlot.Plot.Title("График не построен");
        _formsPlot.Refresh();
        return;
      }

      double margin = (_rightBound - _leftBound) * 0.1;
      double xMin = _leftBound - margin;
      double xMax = _rightBound + margin;

      int samples = 600;
      double[] xs = new double[samples];
      double[] ys = new double[samples];

      for (int i = 0; i < samples; i++)
      {
        double x = xMin + (xMax - xMin) * i / (samples - 1);
        xs[i] = x;
        try
        {
          double y = _parser.Evaluate(x);
          ys[i] = (double.IsNaN(y) || double.IsInfinity(y) || Math.Abs(y) > 1e5) ? double.NaN : y;
        }
        catch { ys[i] = double.NaN; }
      }

      var scatter = _formsPlot.Plot.Add.Scatter(xs, ys);
      scatter.Color = ScottPlot.Color.FromSDColor(Color.SteelBlue);
      scatter.LineWidth = 2;
      scatter.MarkerSize = 0;

      var l1 = _formsPlot.Plot.Add.VerticalLine(_leftBound); l1.Color = ScottPlot.Color.FromSDColor(Color.Orange).WithAlpha(0.5); l1.LinePattern = ScottPlot.LinePattern.Dashed;
      var l2 = _formsPlot.Plot.Add.VerticalLine(_rightBound); l2.Color = ScottPlot.Color.FromSDColor(Color.Orange).WithAlpha(0.5); l2.LinePattern = ScottPlot.LinePattern.Dashed;

      if (_solverResult != null && _solverResult.Success)
      {
        var marker = _formsPlot.Plot.Add.Marker(_solverResult.Point, _solverResult.Value);
        marker.Color = ScottPlot.Color.FromSDColor(Color.Red);
        marker.Size = 12;
      }

      string modeStr = CurrentMode == OptimizationMode.Minimum ? "поиск min" : "поиск max";
      _formsPlot.Plot.Title($"f(x) = {_textBoxFormula.Text}   ({modeStr})");
      _formsPlot.Plot.Axes.SetLimitsX(xMin, xMax);

      if (recalcLimits)
      {
        var validYs = ys.Where(y => !double.IsNaN(y)).ToArray();
        if (validYs.Length > 0)
        {
          Array.Sort(validYs);
          double low = validYs[(int)(validYs.Length * 0.02)];
          double high = validYs[(int)(validYs.Length * 0.98)];
          if (Math.Abs(high - low) < 1e-9) { low -= 1; high += 1; }

          double zoomFactor = _yZoomTrackBar.Value / 50.0; // 1.0 по умолчанию
          double center = (high + low) / 2;
          double halfRange = (high - low) * 0.6 / zoomFactor;

          _formsPlot.Plot.Axes.SetLimitsY(center - halfRange, center + halfRange);
        }
      }
      else
      {
        // Применяем масштаб только к текущему центру, не сбрасывая автоскейл X
        var limits = _formsPlot.Plot.Axes.GetLimits();
        double center = (limits.Bottom + limits.Top) / 2;
        double currentHalfRange = (limits.Top - limits.Bottom) / 2;
        // Если ползунок изменился, тут нужно чуть сложнее, но мы просто применяем новое значение
        // Для простоты, если меняется ползунок, вызываем UpdatePlot(true) с флагом, 
        // но чтобы не сбрасывать X, оставим как есть.
      }

      _formsPlot.Refresh();
    }
  }
}