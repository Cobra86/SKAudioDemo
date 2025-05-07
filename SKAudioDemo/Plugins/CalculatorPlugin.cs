using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace SKAudioDemo.Plugins
{
    public class CalculatorPlugin
    {
        [KernelFunction, Description("Add two numbers")]
        public double Add(
            [Description("First number")] double a,
            [Description("Second number")] double b)
        {
            return a + b;
        }

        [KernelFunction, Description("Subtract two numbers")]
        public double Subtract(
            [Description("First number")] double a,
            [Description("Second number")] double b)
        {
            return a - b;
        }

        [KernelFunction, Description("Multiply two numbers")]
        public double Multiply(
            [Description("First number")] double a,
            [Description("Second number")] double b)
        {
            return a * b;
        }

        [KernelFunction, Description("Divide two numbers")]
        public double Divide(
            [Description("First number (dividend)")] double a,
            [Description("Second number (divisor)")] double b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException("Cannot divide by zero.");
            }

            return a / b;
        }
    }
}
