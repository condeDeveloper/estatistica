using System.Numerics;

using Xunit;

namespace Conde.Estatistica.Testes;

/// <summary>
/// Welford contra a aritmética exata, e contra as fórmulas fechadas da
/// distribuição uniforme.
/// </summary>
/// <remarks>
/// <para>
/// Com dados inteiros, a média e a variância são números racionais exatos, e um
/// <c>BigInteger</c> em cima e outro embaixo dão o valor sem erro nenhum. Esse é
/// o juiz ilimitado: vale para qualquer lista de inteiros, de qualquer tamanho.
/// </para>
/// <para>
/// O segundo juiz são as fórmulas fechadas. A distribuição uniforme sobre 1 até
/// n tem média, variância, assimetria e curtose conhecidas em forma exata, e elas
/// não vêm de implementação nenhuma: vêm de álgebra feita há duzentos anos.
/// </para>
/// </remarks>
public class ResumoTest
{
    /// <summary>Uma fração exata arredondada para o <c>double</c> mais próximo.</summary>
    /// <remarks>
    /// O quociente é calculado com 64 bits de folga além dos 53 que o
    /// <c>double</c> guarda, então o truncamento da divisão só mudaria a resposta
    /// se o valor caísse exatamente na fronteira de arredondamento, o que não
    /// acontece com os dados daqui.
    /// </remarks>
    internal static double Exatamente(BigInteger numerador, BigInteger denominador)
    {
        if (numerador.IsZero)
        {
            return 0.0;
        }

        var negativo = (numerador.Sign < 0) ^ (denominador.Sign < 0);

        numerador = BigInteger.Abs(numerador);
        denominador = BigInteger.Abs(denominador);

        var folga = Math.Max(
            0, 64 + (int)denominador.GetBitLength() - (int)numerador.GetBitLength());

        var resultado = Exato.ParaDouble((numerador << folga) / denominador, -folga);

        return negativo ? -resultado : resultado;
    }

    [Fact(DisplayName = "a formula ingenua da variancia muda de SINAL, e eu errei onde")]
    public void AVarianciaNegativa()
    {
        // Os mesmos quatro numeros, 4, 7, 13 e 16, somados a um deslocamento que
        // cresce. A variancia e 30 em todas as linhas, porque deslocar nao muda a
        // dispersao.
        //
        // Eu escrevi este teste com deslocamento de 10^8 afirmando que a formula
        // da soma dos quadrados daria variancia NEGATIVA. Deu 29,333: errada, e
        // ainda positiva. A ferramenta mostrou onde o sinal vira de verdade, e e
        // uma casa decimal depois.
        static double[] Deslocados(double quanto) =>
            [quanto + 4, quanto + 7, quanto + 13, quanto + 16];

        foreach (var quanto in (double[])[0, 1e3, 1e6, 1e7])
        {
            Assert.Equal(30.0, Resumo.VarianciaIngenua(Deslocados(quanto)), 8);
        }

        // Em 10^8 ela ja erra 2%, e continua positiva.
        Assert.Equal(29.333333333333332, Resumo.VarianciaIngenua(Deslocados(1e8)), 10);

        // Em 10^9 o sinal vira. Variancia negativa e impossivel por definicao: e
        // uma soma de quadrados dividida por um numero positivo.
        var noBilhao = Resumo.VarianciaIngenua(Deslocados(1e9));

        Assert.True(noBilhao < 0, $"em 10^9 a formula ingenua deu {noBilhao}");

        // E em 10^11 ela da zero, que e o jeito mais silencioso de errar: nenhuma
        // excecao, nenhum sinal estranho, so a afirmacao de que os quatro numeros
        // sao todos iguais.
        Assert.Equal(0.0, Resumo.VarianciaIngenua(Deslocados(1e11)));

        // O Welford da 30 em todas elas.
        foreach (var quanto in (double[])[0, 1e3, 1e6, 1e8, 1e9, 1e11, 1e12])
        {
            var resumo = new Resumo();

            resumo.Juntar(Deslocados(quanto));

            Assert.Equal(30.0, resumo.Variancia, 8);
            Assert.Equal(quanto + 10, resumo.Media);
        }
    }

    [Fact(DisplayName = "o erro do Welford segue a CONDICAO do problema, em dez mil listas")]
    public void ContraAFracaoExata()
    {
        // Com dados inteiros a variancia e um numero racional exato, e dois
        // BigInteger dao o valor sem erro nenhum. Esse e o juiz.
        //
        // Mas a primeira versao deste teste exigia erro menor que 64 ulps e
        // falhou com 531 no primeiro caso, e a culpa nao era do Welford: era
        // minha, por nao ter olhado o CONDICIONAMENTO.
        //
        // O condicionamento da variancia e mais ou menos a razao entre a media e
        // o desvio padrao. Dados longe da origem e pouco espalhados sao mal
        // condicionados, e nenhum algoritmo escapa disso: o erro do Welford
        // cresce com a condicao, e o da soma dos quadrados cresce com o QUADRADO
        // dela. E o resultado de Chan, Golub e LeVeque, de 1983, e a ferramenta
        // mede os dois expoentes.
        var sorteio = new Random(29);

        var piorWelford = 0.0;

        var piorIngenua = 0.0;

        var piorCondicao = 0.0;

        for (var caso = 0; caso < 10_000; caso++)
        {
            var quantos = sorteio.Next(2, 60);

            var dados = new double[quantos];

            var deslocamento = (long)sorteio.Next(0, 1_000) * sorteio.Next(1, 1_000_000);

            for (var i = 0; i < quantos; i++)
            {
                dados[i] = deslocamento + sorteio.Next(-1_000, 1_000);
            }

            BigInteger soma = 0, somaDosQuadrados = 0;

            foreach (var valor in dados)
            {
                var inteiro = new BigInteger(valor);

                soma += inteiro;
                somaDosQuadrados += inteiro * inteiro;
            }

            var n = (BigInteger)quantos;

            var media = Exatamente(soma, n);

            // Var = (n.Soma(x^2) - Soma(x)^2) / (n.(n-1)), exata em fracao.
            var variancia = Exatamente(n * somaDosQuadrados - soma * soma, n * (n - 1));

            var resumo = new Resumo();

            resumo.Juntar(dados);

            // A media nao tem problema de condicionamento, mas acumula: cada
            // atualizacao do Welford arredonda uma vez, entao o limite que faz
            // sentido e uma casa por valor somado, e nao um numero fixo.
            //
            // Eu tinha posto 4 aqui e o caso 9.409 errou 5, com 54 valores. O
            // limite errado era meu, nao o algoritmo.
            var erroMedia = Exato.DistanciaEmUlps(resumo.Media, media);

            if (erroMedia > quantos)
            {
                Assert.Fail($"a media errou {erroMedia} ulps com {quantos} valores "
                    + $"no caso {caso}");
            }

            if (variancia <= 0)
            {
                continue;
            }

            var condicao = Math.Sqrt(
                1 + resumo.Media * resumo.Media / resumo.VarianciaDaPopulacao);

            var deWelford = Math.Abs(resumo.Variancia - variancia) / variancia;

            var daIngenua = Math.Abs(Resumo.VarianciaIngenua(dados) - variancia) / variancia;

            // O limite nao e um numero fixo: ele acompanha a condicao, que e o
            // que o problema permite.
            if (deWelford > 1e-14 * condicao)
            {
                Assert.Fail($"o Welford errou {deWelford:0.0e+0} com condicao "
                    + $"{condicao:0.0e+0} no caso {caso}");
            }

            piorWelford = Math.Max(piorWelford, deWelford);
            piorIngenua = Math.Max(piorIngenua, daIngenua);
            piorCondicao = Math.Max(piorCondicao, condicao);
        }

        // E a diferenca entre os dois, que e o motivo de o Welford existir.
        Assert.True(piorCondicao > 1e5, $"a pior condicao foi so {piorCondicao:0.0e+0}");

        Assert.True(piorIngenua > 1_000 * piorWelford,
            $"a ingenua errou {piorIngenua:0.0e+0} e o Welford {piorWelford:0.0e+0}");
    }

    [Fact(DisplayName = "a uniforme de 1 a n, contra as formulas fechadas")]
    public void AUniforme()
    {
        // Juizes que nao vem de implementacao nenhuma: a media, a variancia, a
        // assimetria e a curtose da uniforme discreta tem forma fechada, obtida
        // por algebra ha duzentos anos.
        foreach (var n in (int[])[10, 100, 1_000, 10_000])
        {
            var dados = Enumerable.Range(1, n).Select(i => (double)i).ToArray();

            var resumo = new Resumo();

            resumo.Juntar(dados);

            Assert.Equal(n, resumo.Quantos);
            Assert.Equal(1.0, resumo.Minimo);
            Assert.Equal(n, resumo.Maximo);

            // Media = (n+1)/2.
            Assert.Equal((n + 1) / 2.0, resumo.Media, 9);

            // Variancia da amostra = n(n+1)/12.
            Assert.Equal(n * (n + 1) / 12.0, resumo.Variancia, 6);

            // Variancia da populacao = (n²-1)/12.
            Assert.Equal(((double)n * n - 1) / 12.0, resumo.VarianciaDaPopulacao, 6);

            // A uniforme e simetrica, entao a assimetria e zero.
            Assert.Equal(0.0, resumo.Assimetria, 9);

            // Curtose em excesso = -6(n²+1) / (5(n²-1)). Ela tende a -1,2, que e
            // o valor da uniforme continua.
            var esperada = -6.0 * ((double)n * n + 1) / (5.0 * ((double)n * n - 1));

            Assert.Equal(esperada, resumo.Curtose, 9);
        }

        // E a uniforme continua, no limite: -1,2 exato.
        Assert.Equal(-1.2, -6.0 / 5.0);
    }

    [Fact(DisplayName = "juntar resumos: cortar em qualquer lugar da o mesmo resultado")]
    public void JuntarEmQualquerCorte()
    {
        // A promessa da formula de Chan: calcular em pedacos e juntar tem de dar
        // o mesmo que calcular de uma vez. Aqui isso e conferido em TODOS os
        // cortes possiveis, nao num escolhido a mao.
        var sorteio = new Random(7);

        var dados = new double[200];

        for (var i = 0; i < dados.Length; i++)
        {
            dados[i] = sorteio.NextDouble() * 1_000 - 500;
        }

        var inteiro = new Resumo();

        inteiro.Juntar(dados);

        for (var corte = 0; corte <= dados.Length; corte++)
        {
            var esquerda = new Resumo();
            var direita = new Resumo();

            esquerda.Juntar(dados.AsSpan(0, corte));
            direita.Juntar(dados.AsSpan(corte));

            var juntado = Resumo.Juntar(esquerda, direita);

            Assert.Equal(inteiro.Quantos, juntado.Quantos);
            Assert.Equal(inteiro.Media, juntado.Media, 9);
            Assert.Equal(inteiro.Variancia, juntado.Variancia, 8);
            Assert.Equal(inteiro.Assimetria, juntado.Assimetria, 7);
            Assert.Equal(inteiro.Curtose, juntado.Curtose, 6);
            Assert.Equal(inteiro.Minimo, juntado.Minimo);
            Assert.Equal(inteiro.Maximo, juntado.Maximo);
        }

        // E juntar com o vazio nao muda nada, dos dois lados.
        var vazio = new Resumo();

        Assert.Equal(inteiro.Media, Resumo.Juntar(inteiro, vazio).Media);
        Assert.Equal(inteiro.Media, Resumo.Juntar(vazio, inteiro).Media);
        Assert.Equal(0, Resumo.Juntar(vazio, vazio).Quantos);

        Assert.Throws<ArgumentNullException>(() => Resumo.Juntar(null!, vazio));
        Assert.Throws<ArgumentNullException>(() => Resumo.Juntar(vazio, null!));
    }

    [Fact(DisplayName = "as bordas: zero, um e dois valores")]
    public void AsBordas()
    {
        var vazio = new Resumo();

        Assert.Equal(0, vazio.Quantos);
        Assert.True(double.IsNaN(vazio.Media));
        Assert.True(double.IsNaN(vazio.Variancia));
        Assert.True(double.IsNaN(vazio.Assimetria));
        Assert.True(double.IsNaN(vazio.Curtose));
        Assert.Equal(double.PositiveInfinity, vazio.Minimo);
        Assert.Equal(double.NegativeInfinity, vazio.Maximo);

        var um = new Resumo();

        um.Juntar(42.0);

        Assert.Equal(42.0, um.Media);
        Assert.Equal(0.0, um.VarianciaDaPopulacao);

        // Com um valor so nao ha variancia de amostra: o divisor seria zero.
        Assert.True(double.IsNaN(um.Variancia));

        var dois = new Resumo();

        dois.Juntar([3.0, 7.0]);

        Assert.Equal(5.0, dois.Media);
        Assert.Equal(8.0, dois.Variancia);
        Assert.Equal(4.0, dois.VarianciaDaPopulacao);
        Assert.Equal(Math.Sqrt(8.0), dois.DesvioPadrao);

        // Todos iguais: variancia zero, e a assimetria deixa de existir porque o
        // divisor dela e a variancia.
        var iguais = new Resumo();

        iguais.Juntar(Enumerable.Repeat(5.0, 100).ToArray());

        Assert.Equal(0.0, iguais.Variancia);
        Assert.True(double.IsNaN(iguais.Assimetria));
        Assert.True(double.IsNaN(iguais.Curtose));

        Assert.True(double.IsNaN(Resumo.VarianciaIngenua([1.0])));
    }

    [Fact(DisplayName = "a assimetria conhecida de uma distribuicao torta")]
    public void AAssimetria()
    {
        // Uma distribuicao de dois pontos com probabilidade p e 1-p tem
        // assimetria (1-2p) / raiz(p(1-p)), que e outra formula fechada.
        foreach (var (uns, zeros) in (ValueTuple<int, int>[])
            [(1, 9), (1, 99), (3, 7), (5, 5), (9, 1)])
        {
            var total = uns + zeros;

            var p = (double)uns / total;

            var dados = Enumerable.Repeat(1.0, uns)
                .Concat(Enumerable.Repeat(0.0, zeros))
                .ToArray();

            var resumo = new Resumo();

            resumo.Juntar(dados);

            Assert.Equal(p, resumo.Media, 12);

            // Variancia da populacao = p(1-p).
            Assert.Equal(p * (1 - p), resumo.VarianciaDaPopulacao, 12);

            // Assimetria = (1-2p) / raiz(p(1-p)).
            Assert.Equal((1 - 2 * p) / Math.Sqrt(p * (1 - p)), resumo.Assimetria, 9);

            // Curtose em excesso = (1 - 6p(1-p)) / (p(1-p)).
            Assert.Equal((1 - 6 * p * (1 - p)) / (p * (1 - p)), resumo.Curtose, 8);
        }

        // Com p = 1/2 a distribuicao e simetrica e a assimetria e exatamente zero.
        var simetrica = new Resumo();

        simetrica.Juntar([0.0, 0.0, 1.0, 1.0]);

        Assert.Equal(0.0, simetrica.Assimetria, 12);
    }
}

/// <summary>Açúcar para montar um resumo numa expressão só.</summary>
internal static class ResumoAtalho
{
    public static Resumo Tambem(this Resumo resumo, ReadOnlySpan<double> valores)
    {
        resumo.Juntar(valores);

        return resumo;
    }
}
