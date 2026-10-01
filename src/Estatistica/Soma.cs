namespace Conde.Estatistica;

/// <summary>
/// Quatro maneiras de somar uma lista de números, e elas dão respostas
/// diferentes.
/// </summary>
/// <remarks>
/// <para>
/// Somar parece a operação em que não há o que escolher. Há: o erro da soma
/// ingênua cresce com a quantidade de parcelas, porque cada adição arredonda e os
/// arredondamentos se acumulam. Com um milhão de números a diferença já aparece
/// em dígitos que alguém vai ler.
/// </para>
/// <para>
/// O juiz de todas elas é a soma exata, que está em <see cref="Exato"/>: ela
/// calcula o valor sem erro nenhum e arredonda uma vez só no fim.
/// </para>
/// </remarks>
public static class Soma
{
    /// <summary>A soma direta, um número de cada vez.</summary>
    /// <remarks>
    /// O erro no pior caso cresce com <c>n</c>, e na prática cresce com
    /// <c>raiz de n</c>, porque os arredondamentos se cancelam em parte. É o
    /// bastante para estragar uma média de um milhão de medidas.
    /// </remarks>
    public static double Ingenua(ReadOnlySpan<double> valores)
    {
        var total = 0.0;

        foreach (var valor in valores)
        {
            total += valor;
        }

        return total;
    }

    /// <summary>
    /// A soma compensada de Kahan, que guarda o que se perdeu em cada adição.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ideia de 1965: depois de somar, recupere a parte que não coube e desconte
    /// na próxima parcela. O erro deixa de crescer com <c>n</c> e passa a ser
    /// limitado por uma constante.
    /// </para>
    /// <para>
    /// Ela tem um buraco conhecido, e ele não é teórico: quando a parcela nova é
    /// <b>maior</b> que o acumulado, é a parcela que perde bits, e a compensação
    /// olha para o lado errado. Um dos testes mostra isso com três números.
    /// </para>
    /// </remarks>
    public static double Kahan(ReadOnlySpan<double> valores)
    {
        var total = 0.0;

        var sobra = 0.0;

        foreach (var valor in valores)
        {
            var ajustado = valor - sobra;

            var novo = total + ajustado;

            // O que não coube: a conta parece dar zero em aritmética exata, e é
            // justamente por isso que ela funciona. Um compilador que "otimize"
            // esta linha quebra o algoritmo.
            sobra = novo - total - ajustado;

            total = novo;
        }

        return total;
    }

    /// <summary>
    /// A variante de Neumaier, que trata o caso em que a parcela é maior que o
    /// acumulado.
    /// </summary>
    /// <remarks>
    /// Uma comparação a mais que o Kahan, e ela fecha o buraco: o algoritmo olha
    /// quem é maior e recupera os bits do lado certo. A compensação só entra no
    /// total no fim, o que também evita que ela própria se perca.
    /// </remarks>
    public static double Neumaier(ReadOnlySpan<double> valores)
    {
        var total = 0.0;

        var compensacao = 0.0;

        foreach (var valor in valores)
        {
            var novo = total + valor;

            compensacao += Math.Abs(total) >= Math.Abs(valor)
                ? total - novo + valor
                : valor - novo + total;

            total = novo;
        }

        return total + compensacao;
    }

    /// <summary>
    /// A soma aos pares: divide a lista ao meio e soma cada metade.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sem guardar nada e sem conta extra, só mudando a ordem das adições, o erro
    /// no pior caso cai de <c>n</c> para <c>log n</c>. É o que o NumPy usa por
    /// baixo, e é a solução mais barata das três.
    /// </para>
    /// <para>
    /// Abaixo de um certo tamanho ela volta para a soma direta, porque a recursão
    /// custa mais do que o erro que evitaria.
    /// </para>
    /// </remarks>
    public static double AosPares(ReadOnlySpan<double> valores)
    {
        const int Corte = 128;

        if (valores.Length <= Corte)
        {
            return Ingenua(valores);
        }

        var meio = valores.Length / 2;

        return AosPares(valores[..meio]) + AosPares(valores[meio..]);
    }
}
