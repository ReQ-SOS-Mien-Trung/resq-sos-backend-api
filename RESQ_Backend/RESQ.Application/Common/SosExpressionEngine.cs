using RESQ.Domain.Entities.System;

namespace RESQ.Application.Common;

public static class SosExpressionEngine
{
    private static readonly HashSet<string> BinaryOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADD",
        "SUB",
        "MUL",
        "DIV",
        "MIN",
        "MAX"
    };

    private static readonly HashSet<string> UnaryOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROUND",
        "CEIL",
        "FLOOR"
    };

    public static double Evaluate(
        SosExpressionNode? node,
        IReadOnlyDictionary<string, double> context,
        string expressionName)
    {
        if (node is null)
        {
            throw new InvalidOperationException($"{expressionName} không được để trống.");
        }

        if (!string.IsNullOrWhiteSpace(node.Var))
        {
            var normalizedVariable = SosPriorityRuleConfigSupport.NormalizeKey(node.Var);
            if (!context.TryGetValue(normalizedVariable, out var value))
            {
                throw new InvalidOperationException($"Biến '{normalizedVariable}' không tồn tại trong context của {expressionName}.");
            }

            return value;
        }

        if (string.IsNullOrWhiteSpace(node.Op))
        {
            if (!node.ConstantValue.HasValue)
            {
                throw new InvalidOperationException($"{expressionName} có node hằng số không hợp lệ.");
            }

            return node.ConstantValue.Value;
        }

        var normalizedOperation = SosPriorityRuleConfigSupport.NormalizeKey(node.Op);
        return normalizedOperation switch
        {
            "ADD" => Evaluate(node.Left, context, expressionName) + Evaluate(node.Right, context, expressionName),
            "SUB" => Evaluate(node.Left, context, expressionName) - Evaluate(node.Right, context, expressionName),
            "MUL" => Evaluate(node.Left, context, expressionName) * Evaluate(node.Right, context, expressionName),
            "DIV" => Divide(node, context, expressionName),
            "MIN" => Math.Min(Evaluate(node.Left, context, expressionName), Evaluate(node.Right, context, expressionName)),
            "MAX" => Math.Max(Evaluate(node.Left, context, expressionName), Evaluate(node.Right, context, expressionName)),
            "ROUND" => Math.Round(Evaluate(node.UnaryValue, context, expressionName), 0, MidpointRounding.AwayFromZero),
            "CEIL" => Math.Ceiling(Evaluate(node.UnaryValue, context, expressionName)),
            "FLOOR" => Math.Floor(Evaluate(node.UnaryValue, context, expressionName)),
            _ => throw new InvalidOperationException($"Unsupported operation: {node.Op}")
        };
    }

    public static IReadOnlyList<string> Validate(
        SosExpressionNode? node,
        string expressionName,
        IReadOnlySet<string> allowedVariables)
    {
        var errors = new List<string>();
        ValidateNode(node, expressionName, allowedVariables, errors);
        return errors;
    }

    public static HashSet<string> CollectNormalizedVariables(SosExpressionNode? node)
    {
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectVariables(node, variables);
        return variables;
    }

    private static double Divide(SosExpressionNode node, IReadOnlyDictionary<string, double> context, string expressionName)
    {
        var denominator = Evaluate(node.Right, context, expressionName);
        if (Math.Abs(denominator) < double.Epsilon)
        {
            throw new InvalidOperationException($"Expression '{expressionName}' gây chia cho 0.");
        }

        return Evaluate(node.Left, context, expressionName) / denominator;
    }

    private static void ValidateNode(
        SosExpressionNode? node,
        string path,
        IReadOnlySet<string> allowedVariables,
        List<string> errors)
    {
        if (node is null)
        {
            errors.Add($"{path} không được để trống.");
            return;
        }

        var hasVar = !string.IsNullOrWhiteSpace(node.Var);
        var hasOp = !string.IsNullOrWhiteSpace(node.Op);
        var hasConstant = node.ConstantValue.HasValue;
        var hasUnaryValue = node.UnaryValue is not null;
        var hasLeft = node.Left is not null;
        var hasRight = node.Right is not null;

        if (hasVar)
        {
            if (hasOp || hasConstant || hasUnaryValue || hasLeft || hasRight)
            {
                errors.Add($"{path}: node biến không được chứa op/value/left/right.");
                return;
            }

            var normalizedVariable = SosPriorityRuleConfigSupport.NormalizeKey(node.Var);
            if (!allowedVariables.Contains(normalizedVariable))
            {
                errors.Add($"{path}: biến '{normalizedVariable}' không nằm trong whitelist.");
            }

            return;
        }

        if (!hasOp)
        {
            if (!hasConstant || hasUnaryValue || hasLeft || hasRight)
            {
                errors.Add($"{path}: node hằng số phải có đúng trường value kiểu số.");
            }

            return;
        }

        var normalizedOperation = SosPriorityRuleConfigSupport.NormalizeKey(node.Op);
        if (BinaryOperations.Contains(normalizedOperation))
        {
            if (!hasLeft || !hasRight || hasUnaryValue)
            {
                errors.Add($"{path}: op {normalizedOperation} bắt buộc có left/right và không dùng value.");
                return;
            }

            ValidateNode(node.Left, $"{path}.left", allowedVariables, errors);
            ValidateNode(node.Right, $"{path}.right", allowedVariables, errors);
            return;
        }

        if (UnaryOperations.Contains(normalizedOperation))
        {
            if (!hasUnaryValue || hasLeft || hasRight)
            {
                errors.Add($"{path}: op {normalizedOperation} bắt buộc có value và không dùng left/right.");
                return;
            }

            ValidateNode(node.UnaryValue, $"{path}.value", allowedVariables, errors);
            return;
        }

        errors.Add($"{path}: op '{node.Op}' chưa được hỗ trợ.");
    }

    private static void CollectVariables(SosExpressionNode? node, HashSet<string> variables)
    {
        if (node is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(node.Var))
        {
            variables.Add(SosPriorityRuleConfigSupport.NormalizeKey(node.Var));
        }

        CollectVariables(node.Left, variables);
        CollectVariables(node.Right, variables);
        CollectVariables(node.UnaryValue, variables);
    }
}
