using System;
using System.Data;
namespace XZ;
internal class ProgramData
{
    internal async Task<string> EvalCalculation(string[] args)
    {
        try
        {
            string joined = string.Join("", args);
            Console.WriteLine(ExpressionEvaluator.Evaluate(joined));
            return "ok";
        }
        catch
        {
            return "err";
        }
    }
    internal async Task<string> EvalCalcGetValue(string[] args)
    {
        try
        {
            return ExpressionEvaluator.Evaluate(string.Join("", args)).ToString();
        }
        catch
        {
            return "ERROR";
        }
    }
}