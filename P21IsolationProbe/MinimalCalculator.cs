namespace P21IsolationProbe
{
    /// <summary>
    /// Implementa un'operazione deterministica usata dalla build e dal test offline.
    /// </summary>
    public static class MinimalCalculator
    {
        /// <summary>
        /// Somma due numeri interi.
        /// </summary>
        /// <param name="left">Primo addendo.</param>
        /// <param name="right">Secondo addendo.</param>
        /// <returns>Somma dei due valori.</returns>
        public static int Add(int left, int right)
        {
            return left + right;
        }
    }
}
