using System;
using System.Globalization;

namespace NewtonMethod
{
  public class FunctionParser
  {
    private abstract class Node
    {
      public abstract double Eval(double x);
    }

    private sealed class ConstNode : Node
    {
      private readonly double _value;
      public ConstNode(double value) { _value = value; }
      public override double Eval(double x) { return _value; }
    }

    private sealed class VarNode : Node
    {
      public override double Eval(double x) { return x; }
    }

    private sealed class UnaryNode : Node
    {
      private readonly bool _isNegation;
      private readonly Node _operand;

      public UnaryNode(bool isNegation, Node operand)
      {
        _isNegation = isNegation;
        _operand = operand;
      }

      public override double Eval(double x)
      {
        double value = _operand.Eval(x);
        return _isNegation ? -value : value;
      }
    }

    private sealed class BinaryNode : Node
    {
      private readonly char _operator;
      private readonly Node _leftNode;
      private readonly Node _rightNode;

      public BinaryNode(char op, Node leftNode, Node rightNode)
      {
        _operator = op;
        _leftNode = leftNode;
        _rightNode = rightNode;
      }

      public override double Eval(double x)
      {
        double leftValue = _leftNode.Eval(x);
        double rightValue = _rightNode.Eval(x);

        switch (_operator)
        {
          case '+': return leftValue + rightValue;
          case '-': return leftValue - rightValue;
          case '*': return leftValue * rightValue;
          case '/':
            if (rightValue == 0.0)
              throw new DivideByZeroException("Деление на ноль");
            return leftValue / rightValue;
          case '^': return Math.Pow(leftValue, rightValue);
          default:
            throw new InvalidOperationException("Неизвестный оператор " + _operator);
        }
      }
    }

    private sealed class FuncNode : Node
    {
      private readonly string _functionName;
      private readonly Node _argument;

      public FuncNode(string functionName, Node argument)
      {
        _functionName = functionName;
        _argument = argument;
      }

      public override double Eval(double x)
      {
        double value = _argument.Eval(x);

        switch (_functionName)
        {
          case "sin": return Math.Sin(value);
          case "cos": return Math.Cos(value);
          case "tan": return Math.Tan(value);
          case "exp": return Math.Exp(value);
          case "ln":
            if (value <= 0)
              throw new ArgumentException("ln от неположительного числа");
            return Math.Log(value);
          case "log":
            if (value <= 0)
              throw new ArgumentException("log от неположительного числа");
            return Math.Log10(value);
          case "sqrt":
            if (value < 0)
              throw new ArgumentException("sqrt от отрицательного числа");
            return Math.Sqrt(value);
          case "abs": return Math.Abs(value);
          default:
            throw new FormatException("Неизвестная функция " + _functionName);
        }
      }
    }

    private readonly Node _rootNode;
    private readonly string _expression;
    private int _position;

    public FunctionParser(string expression)
    {
      if (string.IsNullOrWhiteSpace(expression))
        throw new ArgumentException("Формула пустая");

      // В формулах допускаются и точка, и запятая как десятичный разделитель.
      _expression = expression.Replace(" ", "")
                              .Replace(',', '.')
                              .ToLowerInvariant();

      _position = 0;
      _rootNode = ParseExpression();

      if (_position < _expression.Length)
        throw new FormatException(
            "Неожиданный символ '" + _expression[_position] +
            "' в позиции " + _position);
    }

    public double Evaluate(double x)
    {
      double value = _rootNode.Eval(x);
      if (double.IsNaN(value) || double.IsInfinity(value))
        throw new ArithmeticException("Результат вычисления не является конечным числом");
      return value;
    }

    private Node ParseExpression()
    {
      Node leftNode = ParseTerm();

      while (_position < _expression.Length)
      {
        char currentChar = _expression[_position];
        if (currentChar == '+')
        {
          ++_position;
          leftNode = new BinaryNode('+', leftNode, ParseTerm());
        }
        else if (currentChar == '-')
        {
          ++_position;
          leftNode = new BinaryNode('-', leftNode, ParseTerm());
        }
        else
          break;
      }

      return leftNode;
    }

    private Node ParseTerm()
    {
      Node leftNode = ParseUnary();

      while (_position < _expression.Length)
      {
        char currentChar = _expression[_position];
        if (currentChar == '*')
        {
          ++_position;
          leftNode = new BinaryNode('*', leftNode, ParseUnary());
        }
        else if (currentChar == '/')
        {
          ++_position;
          leftNode = new BinaryNode('/', leftNode, ParseUnary());
        }
        else
          break;
      }

      return leftNode;
    }

    // Унарный знак имеет меньший приоритет, чем степень:
    // -x^2 трактуется как -(x^2), а (-x)^2 — как отдельная запись.
    private Node ParseUnary()
    {
      if (_position < _expression.Length && _expression[_position] == '+')
      {
        ++_position;
        return ParseUnary();
      }

      if (_position < _expression.Length && _expression[_position] == '-')
      {
        ++_position;
        return new UnaryNode(true, ParseUnary());
      }

      return ParsePower();
    }

    private Node ParsePower()
    {
      Node baseNode = ParsePrimary();

      if (_position < _expression.Length && _expression[_position] == '^')
      {
        ++_position;
        Node exponentNode = ParseUnary();
        return new BinaryNode('^', baseNode, exponentNode);
      }

      return baseNode;
    }

    private Node ParsePrimary()
    {
      if (_position >= _expression.Length)
        throw new FormatException("Неожиданный конец выражения");

      char currentChar = _expression[_position];

      if (currentChar == '(')
      {
        ++_position;
        Node insideNode = ParseExpression();

        if (_position >= _expression.Length || _expression[_position] != ')')
          throw new FormatException("Не закрыта скобка");

        ++_position;
        return insideNode;
      }

      if (char.IsDigit(currentChar) || currentChar == '.')
        return new ConstNode(ParseNumber());

      if (char.IsLetter(currentChar))
        return ParseIdentifier();

      throw new FormatException("Недопустимый символ '" + currentChar + "'");
    }

    private double ParseNumber()
    {
      int numberStart = _position;
      bool hasDigit = false;

      while (_position < _expression.Length && char.IsDigit(_expression[_position]))
      {
        hasDigit = true;
        ++_position;
      }

      if (_position < _expression.Length && _expression[_position] == '.')
      {
        ++_position;
        while (_position < _expression.Length && char.IsDigit(_expression[_position]))
        {
          hasDigit = true;
          ++_position;
        }
      }

      if (!hasDigit)
        throw new FormatException("Некорректное число");

      // Поддержка научной записи, например 1.2e-3.
      if (_position < _expression.Length && _expression[_position] == 'e')
      {
        int exponentPosition = _position++;
        if (_position < _expression.Length &&
            (_expression[_position] == '+' || _expression[_position] == '-'))
          ++_position;

        int exponentDigits = _position;
        while (_position < _expression.Length && char.IsDigit(_expression[_position]))
          ++_position;

        if (exponentDigits == _position)
          _position = exponentPosition;
      }

      string numberText = _expression.Substring(numberStart, _position - numberStart);
      double parsedValue;

      if (!double.TryParse(numberText, NumberStyles.Float,
                           CultureInfo.InvariantCulture, out parsedValue) ||
          double.IsNaN(parsedValue) || double.IsInfinity(parsedValue))
        throw new FormatException("Некорректное число '" + numberText + "'");

      return parsedValue;
    }

    private Node ParseIdentifier()
    {
      int identifierStart = _position;

      while (_position < _expression.Length && char.IsLetter(_expression[_position]))
        ++_position;

      string identifierName = _expression.Substring(
          identifierStart, _position - identifierStart);

      if (_position < _expression.Length && _expression[_position] == '(')
      {
        ++_position;
        Node argumentNode = ParseExpression();

        if (_position >= _expression.Length || _expression[_position] != ')')
          throw new FormatException("Не закрыта скобка после " + identifierName + "(...)");

        ++_position;

        switch (identifierName)
        {
          case "sin":
          case "cos":
          case "tan":
          case "exp":
          case "ln":
          case "log":
          case "sqrt":
          case "abs":
            return new FuncNode(identifierName, argumentNode);
          default:
            throw new FormatException($"Неизвестная функция '{identifierName}'");
        }
      }

      switch (identifierName)
      {
        case "x": return new VarNode();
        case "pi": return new ConstNode(Math.PI);
        case "e": return new ConstNode(Math.E);
        default:
          throw new FormatException($"Неизвестный идентификатор '{identifierName}'");
      }
    }
  }
}
