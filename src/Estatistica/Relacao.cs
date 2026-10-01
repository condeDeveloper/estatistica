namespace Conde.Estatistica;

/// <summary>O resultado de uma regressão linear simples.</summary>
/// <param name="Intercepto">Onde a reta corta o eixo vertical.</param>
/// <param name="Inclinacao">Quanto o previsto sobe por unidade do previsor.</param>
/// <param name="R2">A fração da variância que a reta explica.</param>
public readonly record struct Reta(double Intercepto, double Inclinacao, double R2)
{
    /// <summary>O valor previsto para um previsor.</summary>
    public double Prever(double x) => Intercepto + Inclinacao * x;
}

/// <summary>
/// Covariância, correlação e regressão linear, pelo mesmo cuidado numérico do
/// resumo.
/// </summary>
/// <remarks>
/// A covariância tem o mesmo problema da variância, e pior: a fórmula ingênua
/// subtrai dois produtos grandes e quase iguais. Aqui ela é acumulada por
/// co-momento, que é o Welford com duas variáveis.
/// </remarks>
public static class Relacao
{
    /// <summary>
    /// A covariância da amostra, acumulada sem formar a soma dos produtos.
    /// </summary>
    public static double Covariancia(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        Conferir(x, y);

        if (x.Length < 2)
        {
            return double.NaN;
        }

        double mediaX = 0, mediaY = 0, coMomento = 0;

        for (var i = 0; i < x.Length; i++)
        {
            var n = i + 1;

            var deltaX = x[i] - mediaX;

            mediaX += deltaX / n;

            mediaY += (y[i] - mediaY) / n;

            // Repare que um dos deltas é de ANTES de atualizar a média e o outro
            // é de depois. Usar os dois do mesmo lado dá uma fórmula que parece
            // simétrica e está errada.
            coMomento += deltaX * (y[i] - mediaY);
        }

        return coMomento / (x.Length - 1);
    }

    /// <summary>
    /// O coeficiente de correlação de Pearson.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É a covariância dividida pelos dois desvios padrão, o que a torna um número
    /// entre -1 e 1 que não muda se as unidades mudarem. Medir altura em metros ou
    /// em polegadas dá exatamente o mesmo valor.
    /// </para>
    /// <para>
    /// Ele mede relação <b>linear</b>, e só. Dados numa parábola perfeita, com
    /// relação determinística, podem dar correlação zero, e um dos testes mostra
    /// exatamente esse caso.
    /// </para>
    /// </remarks>
    public static double Pearson(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        Conferir(x, y);

        if (x.Length < 2)
        {
            return double.NaN;
        }

        var deX = new Resumo();
        var deY = new Resumo();

        deX.Juntar(x);
        deY.Juntar(y);

        var divisor = deX.DesvioPadrao * deY.DesvioPadrao;

        if (divisor == 0)
        {
            return double.NaN;
        }

        var r = Covariancia(x, y) / divisor;

        // O arredondamento pode empurrar o valor um fio para fora do intervalo,
        // e um r de 1,0000000000000002 estraga qualquer conta seguinte que tire
        // a raiz de 1 - r².
        return Math.Clamp(r, -1.0, 1.0);
    }

    /// <summary>
    /// A correlação de postos de Spearman: o Pearson aplicado às posições.
    /// </summary>
    /// <remarks>
    /// Ela mede relação <b>monótona</b> em vez de linear, então dá 1 para qualquer
    /// relação crescente, por mais curva que seja. Empates recebem o posto médio,
    /// que é a convenção que mantém a soma dos postos igual à da sequência sem
    /// empate.
    /// </remarks>
    public static double Spearman(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        Conferir(x, y);

        return Pearson(Postos(x), Postos(y));
    }

    /// <summary>Os postos, com posto médio nos empates.</summary>
    public static double[] Postos(ReadOnlySpan<double> valores)
    {
        var ordem = Enumerable.Range(0, valores.Length).ToArray();

        var copia = valores.ToArray();

        Array.Sort(ordem, (um, outro) => copia[um].CompareTo(copia[outro]));

        var postos = new double[valores.Length];

        var i = 0;

        while (i < ordem.Length)
        {
            var j = i;

            while (j + 1 < ordem.Length && copia[ordem[j + 1]] == copia[ordem[i]])
            {
                j++;
            }

            // A média das posições do bloco empatado, contando de 1.
            var posto = (i + j) / 2.0 + 1;

            for (var k = i; k <= j; k++)
            {
                postos[ordem[k]] = posto;
            }

            i = j + 1;
        }

        return postos;
    }

    /// <summary>
    /// A reta dos mínimos quadrados.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A inclinação é a covariância dividida pela variância do previsor, e o
    /// intercepto sai de a reta passar pelo centro de massa dos pontos. Essas duas
    /// propriedades são invariantes testáveis, e valem mais que comparar com outra
    /// implementação: a reta passa pela média dos dois eixos, e os resíduos somam
    /// zero e são ortogonais ao previsor.
    /// </para>
    /// <para>
    /// O <c>R²</c> é o quadrado do Pearson na regressão simples. Não é por acaso,
    /// e o teste confere.
    /// </para>
    /// </remarks>
    public static Reta Regressao(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        Conferir(x, y);

        if (x.Length < 2)
        {
            throw new ArgumentException("são pelo menos dois pontos", nameof(x));
        }

        var deX = new Resumo();
        var deY = new Resumo();

        deX.Juntar(x);
        deY.Juntar(y);

        if (deX.Variancia == 0)
        {
            throw new ArgumentException(
                "o previsor é constante, e não há reta", nameof(x));
        }

        var inclinacao = Covariancia(x, y) / deX.Variancia;

        var intercepto = deY.Media - inclinacao * deX.Media;

        var r = Pearson(x, y);

        return new Reta(intercepto, inclinacao, double.IsNaN(r) ? 0 : r * r);
    }

    private static void Conferir(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        if (x.Length != y.Length)
        {
            throw new ArgumentException(
                $"as duas listas têm o mesmo tamanho: {x.Length} e {y.Length}", nameof(y));
        }
    }
}
