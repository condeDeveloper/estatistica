namespace Conde.Estatistica;

/// <summary>
/// Média, variância e momentos superiores numa passada só, pelo método de
/// Welford.
/// </summary>
/// <remarks>
/// <para>
/// A fórmula de uma passada que todo mundo aprende é
/// <c>Var = (soma dos quadrados) / n - media²</c>. Ela é algebricamente correta e
/// numericamente desastrosa: os dois termos são enormes e quase iguais, e a
/// subtração entre eles cancela os dígitos significativos. Com dados perto de
/// 10⁸ ela chega a devolver <b>variância negativa</b>, o que é impossível por
/// definição.
/// </para>
/// <para>
/// O método de Welford, de 1962, atualiza a média e a soma dos desvios ao
/// quadrado diretamente, sem nunca formar a soma dos quadrados. Custa o mesmo e
/// não cancela nada.
/// </para>
/// </remarks>
public sealed class Resumo
{
    private double _media;

    private double _m2;

    private double _m3;

    private double _m4;

    /// <summary>Quantos valores entraram.</summary>
    public long Quantos { get; private set; }

    /// <summary>O menor valor visto.</summary>
    public double Minimo { get; private set; } = double.PositiveInfinity;

    /// <summary>O maior valor visto.</summary>
    public double Maximo { get; private set; } = double.NegativeInfinity;

    /// <summary>A média.</summary>
    public double Media => Quantos == 0 ? double.NaN : _media;

    /// <summary>A variância da amostra, com o divisor <c>n - 1</c>.</summary>
    /// <remarks>
    /// O divisor <c>n - 1</c> e não <c>n</c>: a média usada é a da própria
    /// amostra, e não a da população, então um grau de liberdade já foi gasto
    /// para calculá-la. Com <c>n</c> a estimativa sai sistematicamente pequena
    /// demais.
    /// </remarks>
    public double Variancia => Quantos < 2 ? double.NaN : _m2 / (Quantos - 1);

    /// <summary>A variância da população, com o divisor <c>n</c>.</summary>
    public double VarianciaDaPopulacao => Quantos < 1 ? double.NaN : _m2 / Quantos;

    /// <summary>O desvio padrão da amostra.</summary>
    public double DesvioPadrao => Math.Sqrt(Variancia);

    /// <summary>A soma dos desvios ao quadrado em relação à média.</summary>
    public double SomaDosQuadradosDosDesvios => _m2;

    /// <summary>A assimetria.</summary>
    public double Assimetria =>
        Quantos < 3 || _m2 == 0
            ? double.NaN
            : Math.Sqrt((double)Quantos) * _m3 / Math.Pow(_m2, 1.5);

    /// <summary>
    /// A curtose em excesso: zero para a distribuição normal.
    /// </summary>
    /// <remarks>
    /// Subtrair 3 é convenção, e é a que faz a normal valer zero. Sem ela todo
    /// mundo precisa lembrar que o valor de referência é 3, e metade esquece.
    /// </remarks>
    public double Curtose =>
        Quantos < 4 || _m2 == 0 ? double.NaN : Quantos * _m4 / (_m2 * _m2) - 3;

    /// <summary>Junta um valor.</summary>
    public void Juntar(double valor)
    {
        var anteriores = Quantos;

        Quantos++;

        var delta = valor - _media;

        var deltaDividido = delta / Quantos;

        var deltaDivididoAoQuadrado = deltaDividido * deltaDividido;

        var termo = delta * deltaDividido * anteriores;

        _media += deltaDividido;

        _m4 += termo * deltaDivididoAoQuadrado
            * (Quantos * (double)Quantos - 3 * Quantos + 3)
            + 6 * deltaDivididoAoQuadrado * _m2 - 4 * deltaDividido * _m3;

        _m3 += termo * deltaDividido * (Quantos - 2) - 3 * deltaDividido * _m2;

        _m2 += termo;

        Minimo = Math.Min(Minimo, valor);
        Maximo = Math.Max(Maximo, valor);
    }

    /// <summary>Junta uma lista inteira.</summary>
    public void Juntar(ReadOnlySpan<double> valores)
    {
        foreach (var valor in valores)
        {
            Juntar(valor);
        }
    }

    /// <summary>
    /// Junta dois resumos calculados separadamente, pela fórmula de Chan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É o que permite calcular a variância em paralelo: cada pedaço do dado vai
    /// para uma máquina, e os resumos se combinam no fim. Sem isso, uma passada só
    /// significa uma máquina só.
    /// </para>
    /// <para>
    /// A exigência é forte e fácil de conferir: juntar na ordem ou em pedaços tem
    /// de dar o mesmo resultado, qualquer que seja o corte. Um dos testes corta em
    /// todas as posições possíveis.
    /// </para>
    /// </remarks>
    public static Resumo Juntar(Resumo um, Resumo outro)
    {
        ArgumentNullException.ThrowIfNull(um);
        ArgumentNullException.ThrowIfNull(outro);

        if (um.Quantos == 0)
        {
            return Copiar(outro);
        }

        if (outro.Quantos == 0)
        {
            return Copiar(um);
        }

        var total = um.Quantos + outro.Quantos;

        var delta = outro._media - um._media;

        var delta2 = delta * delta;

        var juntado = new Resumo
        {
            Quantos = total,
            _media = um._media + delta * outro.Quantos / total,
            Minimo = Math.Min(um.Minimo, outro.Minimo),
            Maximo = Math.Max(um.Maximo, outro.Maximo),
        };

        juntado._m2 = um._m2 + outro._m2 + delta2 * um.Quantos * outro.Quantos / total;

        juntado._m3 = um._m3 + outro._m3
            + delta2 * delta * um.Quantos * outro.Quantos
                * (um.Quantos - (double)outro.Quantos) / ((double)total * total)
            + 3 * delta * (um.Quantos * outro._m2 - outro.Quantos * um._m2) / total;

        juntado._m4 = um._m4 + outro._m4
            + delta2 * delta2 * um.Quantos * outro.Quantos
                * (um.Quantos * (double)um.Quantos - um.Quantos * (double)outro.Quantos
                    + outro.Quantos * (double)outro.Quantos)
                / ((double)total * total * total)
            + 6 * delta2 * (um.Quantos * (double)um.Quantos * outro._m2
                + outro.Quantos * (double)outro.Quantos * um._m2) / ((double)total * total)
            + 4 * delta * (um.Quantos * outro._m3 - outro.Quantos * um._m3) / total;

        return juntado;
    }

    private static Resumo Copiar(Resumo origem) => new()
    {
        Quantos = origem.Quantos,
        _media = origem._media,
        _m2 = origem._m2,
        _m3 = origem._m3,
        _m4 = origem._m4,
        Minimo = origem.Minimo,
        Maximo = origem.Maximo,
    };

    /// <summary>
    /// A variância pela fórmula da soma dos quadrados, que é a que todo mundo
    /// escreve e ninguém deveria.
    /// </summary>
    /// <remarks>
    /// Está aqui para ser comparada, não para ser usada. Ela é algebricamente
    /// idêntica à de Welford e numericamente outra coisa, e o teste mostra o
    /// tamanho da diferença com quatro números de oito dígitos.
    /// </remarks>
    public static double VarianciaIngenua(ReadOnlySpan<double> valores)
    {
        if (valores.Length < 2)
        {
            return double.NaN;
        }

        double soma = 0, somaDosQuadrados = 0;

        foreach (var valor in valores)
        {
            soma += valor;
            somaDosQuadrados += valor * valor;
        }

        var n = valores.Length;

        return (somaDosQuadrados - soma * soma / n) / (n - 1);
    }
}
