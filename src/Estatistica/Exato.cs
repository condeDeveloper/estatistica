using System.Numerics;

namespace Conde.Estatistica;

/// <summary>
/// O juiz: aritmética exata sobre números de ponto flutuante.
/// </summary>
/// <remarks>
/// <para>
/// Todo <c>double</c> é exatamente uma fração com denominador potência de dois.
/// Isso não é aproximação nem conveniência: é o que o IEEE 754 define. E um
/// número desses cabe inteiro num <c>BigInteger</c> com um expoente do lado.
/// </para>
/// <para>
/// A consequência é um juiz <b>sem limite</b> para qualquer soma de
/// <c>double</c>: dá para calcular o valor exato da soma, sem erro nenhum, e
/// arredondá-lo <b>uma única vez</b> no fim. O resultado é o que a soma deveria
/// ter dado, e qualquer algoritmo de somar pode ser medido contra ele em unidades
/// na última casa.
/// </para>
/// <para>
/// A ressalva honesta: o arredondamento aqui supõe que o resultado cai na faixa
/// normal. Com resultado subnormal pode haver arredondamento duplo, e essa faixa
/// não é exercitada nos testes.
/// </para>
/// </remarks>
public static class Exato
{
    /// <summary>
    /// Abre um <c>double</c> na mantissa inteira e no expoente, sem perder nada.
    /// </summary>
    /// <remarks>
    /// O valor é exatamente <c>mantissa · 2^expoente</c>. Para um número normal a
    /// mantissa guardada no bit a bit vem sem o 1 da frente, que precisa ser
    /// recolocado; para um subnormal esse 1 não existe e o expoente é fixo. Trocar
    /// as duas regras é o engano clássico, e ele some no teste de ida e volta.
    /// </remarks>
    public static (BigInteger Mantissa, int Expoente) Decompor(double valor)
    {
        if (double.IsNaN(valor) || double.IsInfinity(valor))
        {
            throw new ArgumentOutOfRangeException(nameof(valor), valor, "é um número finito");
        }

        var bits = BitConverter.DoubleToInt64Bits(valor);

        var negativo = bits < 0;

        var expoente = (int)(bits >> 52 & 0x7FF);

        var mantissa = bits & 0xF_FFFF_FFFF_FFFFL;

        if (expoente == 0)
        {
            // Subnormal: não há o 1 implícito, e o expoente é o menor possível.
            expoente = -1074;
        }
        else
        {
            mantissa |= 1L << 52;

            // 1023 do viés mais 52 da posição da vírgula.
            expoente -= 1075;
        }

        return (negativo ? -(BigInteger)mantissa : mantissa, expoente);
    }

    /// <summary>
    /// A soma exata de uma lista de <c>double</c>, como <c>mantissa · 2^expoente</c>.
    /// </summary>
    public static (BigInteger Mantissa, int Expoente) Somar(ReadOnlySpan<double> valores)
    {
        if (valores.Length == 0)
        {
            return (BigInteger.Zero, 0);
        }

        var menor = int.MaxValue;

        foreach (var valor in valores)
        {
            var (_, expoente) = Decompor(valor);

            menor = Math.Min(menor, expoente);
        }

        BigInteger total = 0;

        foreach (var valor in valores)
        {
            var (mantissa, expoente) = Decompor(valor);

            total += mantissa << (expoente - menor);
        }

        return (total, menor);
    }

    /// <summary>
    /// Arredonda <c>mantissa · 2^expoente</c> para o <c>double</c> mais próximo,
    /// com desempate no par.
    /// </summary>
    /// <remarks>
    /// É o mesmo critério que o processador usa, e é preciso repeti-lo aqui para
    /// que a comparação seja justa: um juiz que arredondasse para baixo acusaria
    /// erro onde não há.
    /// </remarks>
    public static double ParaDouble(BigInteger mantissa, int expoente)
    {
        if (mantissa.IsZero)
        {
            return 0.0;
        }

        var negativo = mantissa.Sign < 0;

        var abs = BigInteger.Abs(mantissa);

        var bits = (int)abs.GetBitLength();

        var deslocar = bits - 53;

        BigInteger quociente;

        if (deslocar <= 0)
        {
            quociente = abs << -deslocar;
        }
        else
        {
            quociente = abs >> deslocar;

            var resto = abs - (quociente << deslocar);

            var meio = BigInteger.One << (deslocar - 1);

            if (resto > meio || (resto == meio && !quociente.IsEven))
            {
                quociente += 1;
            }
        }

        expoente += deslocar;

        // Arredondar para cima pode levar a mantissa a 2^53, que já não cabe nos
        // 53 bits. Nesse caso ela vira 2^52 e o expoente sobe uma casa.
        if (quociente.GetBitLength() == 54)
        {
            quociente >>= 1;

            expoente += 1;
        }

        var resultado = Math.ScaleB((double)quociente, expoente);

        return negativo ? -resultado : resultado;
    }

    /// <summary>A soma correta: exata por dentro, arredondada uma vez só no fim.</summary>
    public static double Soma(ReadOnlySpan<double> valores)
    {
        var (mantissa, expoente) = Somar(valores);

        return ParaDouble(mantissa, expoente);
    }

    /// <summary>
    /// Quantas casas na última posição separam dois <c>double</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Medir erro em porcentagem engana: perto do zero toda diferença vira enorme,
    /// e perto do infinito toda diferença vira desprezível. A unidade certa é
    /// quantos <c>double</c> representáveis existem entre os dois, e ela se
    /// calcula olhando os bits como inteiros.
    /// </para>
    /// <para>
    /// Zero significa igualdade exata, um significa vizinhos, e aí sim o número é
    /// comparável entre escalas diferentes.
    /// </para>
    /// </remarks>
    public static long DistanciaEmUlps(double um, double outro)
    {
        if (um == outro)
        {
            return 0;
        }

        if (double.IsNaN(um) || double.IsNaN(outro))
        {
            throw new ArgumentOutOfRangeException(nameof(um), "não há distância até um NaN");
        }

        static long ParaOrdem(double valor)
        {
            var bits = BitConverter.DoubleToInt64Bits(valor);

            // Os negativos vêm em ordem decrescente no bit a bit, então precisam
            // ser espelhados para a reta ficar monótona.
            return bits < 0 ? long.MinValue - bits : bits;
        }

        return Math.Abs(ParaOrdem(um) - ParaOrdem(outro));
    }
}
