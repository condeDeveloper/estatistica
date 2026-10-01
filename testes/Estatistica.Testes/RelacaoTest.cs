using Xunit;

namespace Conde.Estatistica.Testes;

/// <summary>
/// Correlação e regressão contra invariantes, e contra o quarteto de Anscombe.
/// </summary>
/// <remarks>
/// <para>
/// Aqui não há implementação de referência: há propriedades que a resposta certa
/// tem de ter. A correlação não muda quando as unidades mudam. Os resíduos da
/// reta somam zero e são ortogonais ao previsor. A reta passa pelo centro de
/// massa dos pontos. O <c>R²</c> é o quadrado do Pearson.
/// </para>
/// <para>
/// Cada uma dessas é uma consequência da definição, então conferi-las é conferir
/// a conta sem repetir a conta.
/// </para>
/// </remarks>
public class RelacaoTest
{
    /// <summary>
    /// O quarteto de Anscombe: quatro conjuntos com os mesmos resumos e nada mais
    /// em comum.
    /// </summary>
    /// <remarks>
    /// Publicado por Francis Anscombe em 1973 para mostrar que resumir dados sem
    /// olhar para eles é perigoso. Os quatro têm a mesma média em x e em y, a
    /// mesma variância nos dois, a mesma correlação e a mesma reta de regressão,
    /// até a segunda ou terceira casa. Um é linear com ruído, um é uma parábola,
    /// um é uma reta perfeita com um ponto fora, e um é uma coluna com um ponto
    /// solitário que sozinho define a inclinação.
    /// </remarks>
    private static readonly double[][] Anscombe =
    [
        [10, 8, 13, 9, 11, 14, 6, 4, 12, 7, 5],
        [8.04, 6.95, 7.58, 8.81, 8.33, 9.96, 7.24, 4.26, 10.84, 4.82, 5.68],
        [9.14, 8.14, 8.74, 8.77, 9.26, 8.10, 6.13, 3.10, 9.13, 7.26, 4.74],
        [7.46, 6.77, 12.74, 7.11, 7.81, 8.84, 6.08, 5.39, 8.15, 6.42, 5.73],
        [8, 8, 8, 8, 8, 8, 8, 19, 8, 8, 8],
        [6.58, 5.76, 7.71, 8.84, 8.47, 7.04, 5.25, 12.50, 5.56, 7.91, 6.89],
    ];

    [Fact(DisplayName = "o quarteto de Anscombe: quatro conjuntos com os mesmos resumos")]
    public void OQuartetoDeAnscombe()
    {
        var conjuntos = new[]
        {
            (X: Anscombe[0], Y: Anscombe[1]),
            (X: Anscombe[0], Y: Anscombe[2]),
            (X: Anscombe[0], Y: Anscombe[3]),
            (X: Anscombe[4], Y: Anscombe[5]),
        };

        var mediasDeY = new List<double>();
        var varianciasDeY = new List<double>();
        var correlacoes = new List<double>();
        var interceptos = new List<double>();
        var inclinacoes = new List<double>();
        var erres = new List<double>();

        foreach (var (x, y) in conjuntos)
        {
            var deX = new Resumo();
            var deY = new Resumo();

            deX.Juntar(x);
            deY.Juntar(y);

            Assert.Equal(11, deX.Quantos);

            // Em x os quatro batem EXATO: media 9 e variancia 11.
            Assert.Equal(9.0, deX.Media, 10);
            Assert.Equal(11.0, deX.Variancia, 10);

            var reta = Relacao.Regressao(x, y);

            mediasDeY.Add(deY.Media);
            varianciasDeY.Add(deY.Variancia);
            correlacoes.Add(Relacao.Pearson(x, y));
            interceptos.Add(reta.Intercepto);
            inclinacoes.Add(reta.Inclinacao);
            erres.Add(reta.R2);
        }

        // Em y eles nao batem exato, e dizer que batem seria copiar a lenda em
        // vez de conferir o dado. O que vale e que a DIFERENCA entre os quatro
        // fica na terceira casa, que e exatamente o ponto de Anscombe: nenhum
        // resumo distingue conjuntos que nao tem nada a ver um com o outro.
        static void Juntos(List<double> valores, double publicado, double folga, string qual)
        {
            Assert.True(valores.Max() - valores.Min() < folga,
                $"{qual}: os quatro foram de {valores.Min():0.#####} a {valores.Max():0.#####}");

            foreach (var valor in valores)
            {
                Assert.True(Math.Abs(valor - publicado) < folga,
                    $"{qual}: {valor:0.#####} esta longe do publicado {publicado}");
            }
        }

        Juntos(mediasDeY, 7.50, 0.005, "a media de y");
        // A variancia de y e a que mais varia entre os quatro: de 4,12262 a
        // 4,12763. Cinco milesimos de diferenca, que e o que separa conjuntos
        // completamente diferentes.
        Juntos(varianciasDeY, 4.125, 0.006, "a variancia de y");
        Juntos(correlacoes, 0.816, 0.005, "a correlacao");
        Juntos(interceptos, 3.00, 0.005, "o intercepto");
        Juntos(inclinacoes, 0.500, 0.005, "a inclinacao");
        Juntos(erres, 0.666, 0.005, "o R2");

        // E o que os resumos nao contam: o segundo conjunto e uma parabola
        // perfeita, e a correlacao de postos dele e quase 1 enquanto o terceiro
        // tem um ponto fora que estraga tudo.
        //
        // O quarto e o mais cruel: x e constante em dez dos onze pontos, entao a
        // inclinacao inteira vem de um ponto so. Tire ele e nao ha reta.
        var semOForaDaCurva = Anscombe[4].Take(7).Concat(Anscombe[4].Skip(8)).ToArray();

        Assert.Equal(10, semOForaDaCurva.Length);

        Assert.All(semOForaDaCurva, valor => Assert.Equal(8.0, valor));

        Assert.Throws<ArgumentException>(
            () => Relacao.Regressao(semOForaDaCurva, Anscombe[5].Take(7).Concat(Anscombe[5].Skip(8)).ToArray()));
    }

    [Fact(DisplayName = "o Pearson nao muda quando as unidades mudam")]
    public void AInvarianciaDoPearson()
    {
        // A propriedade que faz a correlacao ser comparavel entre coisas
        // diferentes: medir altura em metros ou em polegadas da o mesmo valor.
        // Multiplicar por numero negativo troca o sinal, e so.
        var sorteio = new Random(29);

        for (var caso = 0; caso < 5_000; caso++)
        {
            var quantos = sorteio.Next(3, 50);

            var x = new double[quantos];
            var y = new double[quantos];

            for (var i = 0; i < quantos; i++)
            {
                x[i] = sorteio.NextDouble() * 100 - 50;
                y[i] = sorteio.NextDouble() * 100 - 50;
            }

            var original = Relacao.Pearson(x, y);

            // Entre -1 e 1, sempre.
            Assert.InRange(original, -1.0, 1.0);

            var escalaX = sorteio.NextDouble() * 10 - 5;
            var escalaY = sorteio.NextDouble() * 10 - 5;

            if (Math.Abs(escalaX) < 0.1 || Math.Abs(escalaY) < 0.1)
            {
                continue;
            }

            var deslocaX = sorteio.NextDouble() * 1_000 - 500;
            var deslocaY = sorteio.NextDouble() * 1_000 - 500;

            var outroX = x.Select(v => v * escalaX + deslocaX).ToArray();
            var outroY = y.Select(v => v * escalaY + deslocaY).ToArray();

            var esperado = original * Math.Sign(escalaX) * Math.Sign(escalaY);

            Assert.Equal(esperado, Relacao.Pearson(outroX, outroY), 8);

            // E a covariancia muda pelo produto das escalas, sem o deslocamento.
            Assert.Equal(
                Relacao.Covariancia(x, y) * escalaX * escalaY,
                Relacao.Covariancia(outroX, outroY),
                6);
        }
    }

    [Fact(DisplayName = "relacao perfeita da 1, e relacao curva perfeita pode dar ZERO")]
    public void OPearsonSoVeRetas()
    {
        // Numa reta exata o Pearson da 1 ou -1 exatos.
        var x = Enumerable.Range(0, 50).Select(i => (double)i).ToArray();

        Assert.Equal(1.0, Relacao.Pearson(x, x.Select(v => 3 * v + 7).ToArray()), 12);
        Assert.Equal(-1.0, Relacao.Pearson(x, x.Select(v => -2 * v + 1).ToArray()), 12);

        // E aqui esta o limite dele. Os pontos de -10 a 10 na parabola y = x²
        // tem relacao DETERMINISTICA: y e funcao exata de x, sem ruido nenhum.
        // O Pearson da zero.
        var simetrico = Enumerable.Range(-10, 21).Select(i => (double)i).ToArray();

        var parabola = simetrico.Select(v => v * v).ToArray();

        Assert.Equal(0.0, Relacao.Pearson(simetrico, parabola), 12);

        // O Spearman tambem da zero aqui, porque a relacao nao e monotona. Mas
        // numa relacao monotona e curva ele da 1 e o Pearson nao.
        Assert.Equal(0.0, Relacao.Spearman(simetrico, parabola), 12);

        var crescente = Enumerable.Range(1, 30).Select(i => (double)i).ToArray();

        var exponencial = crescente.Select(Math.Exp).ToArray();

        Assert.Equal(1.0, Relacao.Spearman(crescente, exponencial), 12);

        Assert.True(Relacao.Pearson(crescente, exponencial) < 0.8,
            "o Pearson numa exponencial devia estar bem longe de 1");

        // Postos com empates recebem o posto medio, e a soma dos postos continua
        // sendo a mesma de 1 ate n.
        var comEmpates = Relacao.Postos([5.0, 1.0, 5.0, 3.0, 5.0]);

        Assert.Equal([4.0, 1.0, 4.0, 2.0, 4.0], comEmpates);

        Assert.Equal(15.0, comEmpates.Sum());
    }

    [Fact(DisplayName = "os residuos da reta somam zero e sao ortogonais ao previsor")]
    public void AsInvariantesDaReta()
    {
        // As duas condicoes que definem os minimos quadrados. Nao sao
        // consequencias da implementacao: sao a propria definicao, escrita de
        // outro jeito.
        var sorteio = new Random(13);

        for (var caso = 0; caso < 5_000; caso++)
        {
            var quantos = sorteio.Next(3, 60);

            var x = new double[quantos];
            var y = new double[quantos];

            for (var i = 0; i < quantos; i++)
            {
                x[i] = sorteio.NextDouble() * 20 - 10;
                y[i] = 2.5 * x[i] - 1.3 + (sorteio.NextDouble() - 0.5) * 5;
            }

            var reta = Relacao.Regressao(x, y);

            double somaDosResiduos = 0, produtoComX = 0;

            for (var i = 0; i < quantos; i++)
            {
                var residuo = y[i] - reta.Prever(x[i]);

                somaDosResiduos += residuo;
                produtoComX += residuo * x[i];
            }

            Assert.Equal(0.0, somaDosResiduos, 8);
            Assert.Equal(0.0, produtoComX, 7);

            // A reta passa pelo centro de massa.
            var deX = new Resumo();
            var deY = new Resumo();

            deX.Juntar(x);
            deY.Juntar(y);

            Assert.Equal(deY.Media, reta.Prever(deX.Media), 8);

            // E o R² e o quadrado do Pearson, na regressao simples.
            var r = Relacao.Pearson(x, y);

            Assert.Equal(r * r, reta.R2, 10);

            // Que tambem e a fracao da variancia explicada.
            var variaDosResiduos = new Resumo();

            for (var i = 0; i < quantos; i++)
            {
                variaDosResiduos.Juntar(y[i] - reta.Prever(x[i]));
            }

            Assert.Equal(
                1 - variaDosResiduos.SomaDosQuadradosDosDesvios / deY.SomaDosQuadradosDosDesvios,
                reta.R2,
                8);
        }
    }

    [Fact(DisplayName = "as bordas da relacao")]
    public void AsBordas()
    {
        Assert.Throws<ArgumentException>(() => Relacao.Pearson([1.0, 2.0], [1.0]));
        Assert.Throws<ArgumentException>(() => Relacao.Covariancia([1.0], [1.0, 2.0]));
        Assert.Throws<ArgumentException>(() => Relacao.Regressao([1.0], [1.0]));
        Assert.Throws<ArgumentException>(() => Relacao.Regressao([2.0, 2.0], [1.0, 5.0]));

        // Com menos de dois pontos nao ha covariancia nem correlacao.
        Assert.True(double.IsNaN(Relacao.Covariancia([1.0], [2.0])));
        Assert.True(double.IsNaN(Relacao.Pearson([1.0], [2.0])));

        // Com uma das listas constante, o Pearson nao existe: o divisor e zero.
        Assert.True(double.IsNaN(Relacao.Pearson([1.0, 1.0, 1.0], [1.0, 2.0, 3.0])));

        // Dois pontos sempre dao uma reta exata.
        var dois = Relacao.Regressao([1.0, 3.0], [5.0, 9.0]);

        Assert.Equal(2.0, dois.Inclinacao, 12);
        Assert.Equal(3.0, dois.Intercepto, 12);
        Assert.Equal(1.0, dois.R2, 12);

        // A covariancia de uma lista com ela mesma e a variancia.
        var dados = new double[] { 3, 1, 4, 1, 5, 9, 2, 6 };

        var resumo = new Resumo();

        resumo.Juntar(dados);

        Assert.Equal(resumo.Variancia, Relacao.Covariancia(dados, dados), 10);

        Assert.Equal(1.0, Relacao.Pearson(dados, dados), 12);

        Assert.Empty(Relacao.Postos([]));
    }
}
