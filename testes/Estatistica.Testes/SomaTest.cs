using System.Numerics;

using Xunit;

namespace Conde.Estatistica.Testes;

/// <summary>
/// O juiz é a soma exata, e ela não tem limite.
/// </summary>
/// <remarks>
/// <para>
/// Testar ponto flutuante contra ponto flutuante é andar em círculo: a referência
/// tem o mesmo erro do que se quer medir. Aqui a referência é outra coisa.
/// </para>
/// <para>
/// Todo <c>double</c> é exatamente uma fração com denominador potência de dois,
/// por definição do IEEE 754. Então a soma de uma lista deles tem um valor exato,
/// que cabe num <c>BigInteger</c>, e esse valor arredondado uma vez só é o que a
/// soma <b>deveria</b> ter dado. Qualquer algoritmo de somar pode ser medido
/// contra ele, em qualquer entrada, sem tabela nem tolerância inventada.
/// </para>
/// </remarks>
public class SomaTest
{
    [Fact(DisplayName = "abrir e remontar um double nao perde nada, em 200 mil valores")]
    public void AIdaEAVolta()
    {
        var sorteio = new Random(29);

        var especiais = new[]
        {
            0.0, -0.0, 1.0, -1.0, 0.1, double.Epsilon, -double.Epsilon,
            double.MaxValue, double.MinValue, 1e308, -1e-308,
            Math.PI, Math.E, 1.0 / 3, 4.9e-324, 2.2250738585072014e-308,
        };

        foreach (var valor in especiais)
        {
            var (mantissa, expoente) = Exato.Decompor(valor);

            Assert.Equal(valor, Exato.ParaDouble(mantissa, expoente));
        }

        for (var caso = 0; caso < 200_000; caso++)
        {
            var bits = (long)((ulong)sorteio.NextInt64() ^ ((ulong)sorteio.NextInt64() << 32));

            var valor = BitConverter.Int64BitsToDouble(bits);

            if (double.IsNaN(valor) || double.IsInfinity(valor))
            {
                continue;
            }

            var (mantissa, expoente) = Exato.Decompor(valor);

            if (Exato.ParaDouble(mantissa, expoente) != valor)
            {
                Assert.Fail($"{valor:R} voltou como {Exato.ParaDouble(mantissa, expoente):R}");
            }
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => Exato.Decompor(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Exato.Decompor(double.PositiveInfinity));
    }

    [Fact(DisplayName = "a distancia em ulps: zero para iguais, um para vizinhos")]
    public void ADistanciaEmUlps()
    {
        Assert.Equal(0, Exato.DistanciaEmUlps(1.0, 1.0));
        Assert.Equal(1, Exato.DistanciaEmUlps(1.0, Math.BitIncrement(1.0)));
        Assert.Equal(1, Exato.DistanciaEmUlps(1.0, Math.BitDecrement(1.0)));

        // O zero negativo e o positivo sao iguais na comparacao, e e isso que a
        // distancia tem de dizer.
        Assert.Equal(0, Exato.DistanciaEmUlps(0.0, -0.0));

        // E a travessia do zero: do menor positivo ao menor negativo sao dois
        // passos, porque o zero fica no meio.
        Assert.Equal(2, Exato.DistanciaEmUlps(double.Epsilon, -double.Epsilon));

        var sorteio = new Random(7);

        for (var caso = 0; caso < 50_000; caso++)
        {
            var valor = sorteio.NextDouble() * Math.Pow(10, sorteio.Next(-200, 200));

            var depois = valor;

            var passos = sorteio.Next(0, 20);

            for (var i = 0; i < passos; i++)
            {
                depois = Math.BitIncrement(depois);
            }

            Assert.Equal(passos, Exato.DistanciaEmUlps(valor, depois));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => Exato.DistanciaEmUlps(double.NaN, 1));
    }

    [Fact(DisplayName = "o caso de tres numeros em que o Kahan erra e o Neumaier acerta")]
    public void OBuracoDoKahan()
    {
        // A soma compensada de Kahan e famosa, e tem um buraco que nao e teorico:
        // quando a parcela nova e MAIOR que o acumulado, e a parcela que perde
        // bits, e a compensacao olha para o lado errado.
        //
        // Tres numeros bastam. A resposta certa e 1.
        double[] tres = [1e16, 1.0, -1e16];

        Assert.Equal(1.0, Exato.Soma(tres));

        Assert.Equal(0.0, Soma.Ingenua(tres));

        // O Kahan tambem da zero, e esse e o ponto.
        Assert.Equal(0.0, Soma.Kahan(tres));

        // Uma comparacao a mais, e o Neumaier acerta.
        Assert.Equal(1.0, Soma.Neumaier(tres));

        // O exemplo classico da literatura, com quatro numeros e expoente 100.
        double[] quatro = [1.0, 1e100, 1.0, -1e100];

        Assert.Equal(2.0, Exato.Soma(quatro));
        Assert.Equal(0.0, Soma.Ingenua(quatro));
        Assert.Equal(0.0, Soma.Kahan(quatro));
        Assert.Equal(2.0, Soma.Neumaier(quatro));

        // E o caso em que todos acertam, para o teste nao estar medindo o nada.
        double[] faceis = [1.0, 2.0, 3.0, 4.0];

        Assert.Equal(10.0, Soma.Ingenua(faceis));
        Assert.Equal(10.0, Soma.Kahan(faceis));
        Assert.Equal(10.0, Soma.Neumaier(faceis));
        Assert.Equal(10.0, Soma.AosPares(faceis));
        Assert.Equal(10.0, Exato.Soma(faceis));

        Assert.Equal(0.0, Exato.Soma([]));
    }

    [Fact(DisplayName = "um milhao de parcelas: quantos ulps cada algoritmo erra")]
    public void UmMilhaoDeParcelas()
    {
        // O erro da soma ingenua cresce com a quantidade de parcelas. Aqui isso
        // vira um numero, e o numero e deterministico: a semente e fixa e a conta
        // da o mesmo em toda maquina.
        var sorteio = new Random(29);

        var valores = new double[1_000_000];

        for (var i = 0; i < valores.Length; i++)
        {
            // Escalas misturadas, que e o que faz o erro aparecer. Com todos os
            // numeros do mesmo tamanho nao ha o que cancelar.
            valores[i] = (sorteio.NextDouble() - 0.5) * Math.Pow(10, sorteio.Next(-6, 7));
        }

        var certo = Exato.Soma(valores);

        var daIngenua = Exato.DistanciaEmUlps(Soma.Ingenua(valores), certo);

        var doKahan = Exato.DistanciaEmUlps(Soma.Kahan(valores), certo);

        var doNeumaier = Exato.DistanciaEmUlps(Soma.Neumaier(valores), certo);

        var dosPares = Exato.DistanciaEmUlps(Soma.AosPares(valores), certo);

        // A ingenua erra, e erra muito mais que as outras tres.
        Assert.True(daIngenua > 100,
            $"a ingenua errou so {daIngenua} ulps, e o teste deixou de medir alguma coisa");

        Assert.True(doKahan <= 1, $"o Kahan errou {doKahan} ulps");
        Assert.True(doNeumaier <= 1, $"o Neumaier errou {doNeumaier} ulps");

        // A soma aos pares nao guarda nada e nao faz conta extra: so muda a ordem
        // das adicoes. Mesmo assim ela chega perto das compensadas.
        Assert.True(dosPares < daIngenua / 10,
            $"os pares erraram {dosPares} e a ingenua {daIngenua}");
    }

    [Fact(DisplayName = "varredura: dez mil listas sorteadas contra a soma exata")]
    public void AVarredura()
    {
        // Nao e um caso escolhido a mao: sao dez mil listas de tamanho e escala
        // sorteados, cada uma conferida contra o valor exato.
        var sorteio = new Random(13);

        long piorIngenua = 0, piorKahan = 0, piorNeumaier = 0, piorPares = 0;

        for (var caso = 0; caso < 10_000; caso++)
        {
            var quantos = sorteio.Next(1, 400);

            var valores = new double[quantos];

            var escala = sorteio.Next(0, 30);

            for (var i = 0; i < quantos; i++)
            {
                valores[i] = (sorteio.NextDouble() - 0.5) * Math.Pow(10, sorteio.Next(-escala, escala + 1));
            }

            var certo = Exato.Soma(valores);

            piorIngenua = Math.Max(piorIngenua, Exato.DistanciaEmUlps(Soma.Ingenua(valores), certo));
            piorKahan = Math.Max(piorKahan, Exato.DistanciaEmUlps(Soma.Kahan(valores), certo));
            piorNeumaier = Math.Max(piorNeumaier, Exato.DistanciaEmUlps(Soma.Neumaier(valores), certo));
            piorPares = Math.Max(piorPares, Exato.DistanciaEmUlps(Soma.AosPares(valores), certo));
        }

        // O Neumaier e o unico que fica em zero ou um ulp em TODAS as dez mil.
        Assert.True(piorNeumaier <= 1, $"o pior do Neumaier foi {piorNeumaier} ulps");

        // Os outros tres saem do lugar, e o teste exige que saiam: se um deles
        // ficasse em zero, seria sinal de que as listas sorteadas nao exercitam
        // o problema.
        Assert.True(piorIngenua > 1, $"o pior da ingenua foi {piorIngenua} ulps");
        Assert.True(piorKahan > 1, $"o pior do Kahan foi {piorKahan} ulps");
        Assert.True(piorPares > 1, $"o pior dos pares foi {piorPares} ulps");
    }

    [Fact(DisplayName = "a soma exata contra a conta feita com fracoes, em listas pequenas")]
    public void ContraAFracao()
    {
        // Um segundo caminho ate o mesmo numero: somar as fracoes exatas com
        // numerador e denominador separados, em vez de alinhar expoentes. Se os
        // dois concordam em cem mil listas, o erro teria de estar nos dois.
        var sorteio = new Random(29);

        for (var caso = 0; caso < 100_000; caso++)
        {
            var quantos = sorteio.Next(1, 8);

            var valores = new double[quantos];

            for (var i = 0; i < quantos; i++)
            {
                valores[i] = (sorteio.NextDouble() - 0.5) * Math.Pow(2, sorteio.Next(-40, 40));
            }

            BigInteger numerador = 0;

            BigInteger denominador = 1;

            foreach (var valor in valores)
            {
                var (mantissa, expoente) = Exato.Decompor(valor);

                BigInteger parcelaNum = mantissa;

                BigInteger parcelaDen = 1;

                if (expoente >= 0)
                {
                    parcelaNum <<= expoente;
                }
                else
                {
                    parcelaDen <<= -expoente;
                }

                numerador = numerador * parcelaDen + parcelaNum * denominador;

                denominador *= parcelaDen;
            }

            var (soma, escala) = Exato.Somar(valores);

            // numerador/denominador tem de ser igual a soma · 2^escala.
            BigInteger esquerda = numerador;

            BigInteger direita = soma * denominador;

            if (escala >= 0)
            {
                direita <<= escala;
            }
            else
            {
                esquerda <<= -escala;
            }

            if (esquerda != direita)
            {
                Assert.Fail($"as duas contas discordaram no caso {caso}");
            }
        }
    }
}
