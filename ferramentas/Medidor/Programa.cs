using System.Diagnostics;
using System.Numerics;

using Conde.Estatistica;

// A ferramenta de medida. Ela REGISTRA numeros; nenhuma afirmacao de tempo mora
// na suite de testes, porque uma afirmacao de relogio falha sem que nada tenha
// piorado, basta a maquina do CI estar ocupada. Os erros em ulps, que sao
// deterministicos, esses sim estao nos testes.

var comando = args.Length > 0 ? args[0] : "ajuda";

switch (comando)
{
    case "cancelamento": Cancelamento(); break;
    case "condicionamento": Condicionamento(); break;
    case "somas": Somas(); break;
    case "custo": Custo(); break;
    default:
        Console.WriteLine("uso: medidor <cancelamento|condicionamento|somas|custo>");
        break;
}

static void Cancelamento()
{
    Console.WriteLine();
    Console.WriteLine("  Quatro numeros, sempre os mesmos quatro: 4, 7, 13 e 16, somados a um");
    Console.WriteLine("  deslocamento que cresce. A variancia e 30 em todas as linhas, porque");
    Console.WriteLine("  deslocar nao muda a dispersao.");
    Console.WriteLine();
    Console.WriteLine("    deslocamento      soma dos quadrados          Welford");
    Console.WriteLine("  " + new string('-', 62));

    for (var potencia = 0; potencia <= 12; potencia++)
    {
        var deslocamento = Math.Pow(10, potencia);

        double[] dados =
        [
            deslocamento + 4, deslocamento + 7, deslocamento + 13, deslocamento + 16,
        ];

        var resumo = new Resumo();

        resumo.Juntar(dados);

        Console.WriteLine($"    10^{potencia,-2}          {Resumo.VarianciaIngenua(dados),20:0.############}"
            + $"   {resumo.Variancia,14:0.############}");
    }

    Console.WriteLine();
    Console.WriteLine("  A formula da soma dos quadrados perde um digito a cada potencia de dez");
    Console.WriteLine("  de deslocamento, porque ela subtrai dois numeros enormes e quase iguais.");
    Console.WriteLine("  O Welford nunca forma esses dois numeros.");
    Console.WriteLine();
}

static void Condicionamento()
{
    Console.WriteLine();
    Console.WriteLine("  O erro de qualquer algoritmo de variancia tem um piso, e o piso e o");
    Console.WriteLine("  CONDICIONAMENTO do problema: a razao entre a media e o desvio padrao.");
    Console.WriteLine("  Dados longe da origem e pouco espalhados sao mal condicionados, e nenhum");
    Console.WriteLine("  algoritmo escapa disso.");
    Console.WriteLine();
    Console.WriteLine("        media   desvio   condicao     Welford    soma dos quadrados");
    Console.WriteLine("  " + new string('-', 72));

    var sorteio = new Random(29);

    foreach (var deslocamento in (double[])[0, 1e3, 1e6, 1e9, 1e12])
    {
        var dados = new double[1_000];

        for (var i = 0; i < dados.Length; i++)
        {
            dados[i] = deslocamento + sorteio.Next(-1_000, 1_000);
        }

        // A variancia exata, em fracao de inteiros.
        BigInteger soma = 0, somaDosQuadrados = 0;

        foreach (var valor in dados)
        {
            var inteiro = new BigInteger(valor);

            soma += inteiro;
            somaDosQuadrados += inteiro * inteiro;
        }

        var n = (BigInteger)dados.Length;

        var exata = Exatamente(n * somaDosQuadrados - soma * soma, n * (n - 1));

        var resumo = new Resumo();

        resumo.Juntar(dados);

        var condicao = Math.Sqrt(1 + resumo.Media * resumo.Media / resumo.VarianciaDaPopulacao);

        var deWelford = Math.Abs(resumo.Variancia - exata) / exata;

        var daIngenua = Math.Abs(Resumo.VarianciaIngenua(dados) - exata) / exata;

        Console.WriteLine($"  {resumo.Media,11:0.0e+0}  {resumo.DesvioPadrao,7:0.0}  {condicao,9:0.0e+0}"
            + $"  {deWelford,10:0.0e+0}  {daIngenua,20:0.0e+0}");
    }

    Console.WriteLine();
    Console.WriteLine("  O erro do Welford cresce com a condicao, e o da soma dos quadrados");
    Console.WriteLine("  cresce com o QUADRADO dela. E o resultado de Chan, Golub e LeVeque, de");
    Console.WriteLine("  1983, e e por isso que a diferenca entre os dois so aparece longe da");
    Console.WriteLine("  origem: perto dela a condicao e 1 e os dois acertam.");
    Console.WriteLine();
}

static double Exatamente(BigInteger numerador, BigInteger denominador)
{
    if (numerador.IsZero)
    {
        return 0.0;
    }

    var folga = Math.Max(
        0, 64 + (int)denominador.GetBitLength() - (int)numerador.GetBitLength());

    return Exato.ParaDouble((numerador << folga) / denominador, -folga);
}

static void Somas()
{
    Console.WriteLine();
    Console.WriteLine("  Somar um milhao de numeros de escalas misturadas. O erro e medido em");
    Console.WriteLine("  ulps contra a soma EXATA, calculada em inteiro grande e arredondada uma");
    Console.WriteLine("  vez so no fim.");
    Console.WriteLine();

    var sorteio = new Random(29);

    var valores = new double[1_000_000];

    for (var i = 0; i < valores.Length; i++)
    {
        valores[i] = (sorteio.NextDouble() - 0.5) * Math.Pow(10, sorteio.Next(-6, 7));
    }

    var certo = Exato.Soma(valores);

    Console.WriteLine("     algoritmo      erro em ulps        tempo");
    Console.WriteLine("  " + new string('-', 50));

    foreach (var (nome, somar) in (ValueTuple<string, Func<double[], double>>[])
        [
            ("ingenua", v => Soma.Ingenua(v)),
            ("Kahan", v => Soma.Kahan(v)),
            ("Neumaier", v => Soma.Neumaier(v)),
            ("aos pares", v => Soma.AosPares(v)),
            ("exata", v => Exato.Soma(v)),
        ])
    {
        somar(valores);

        var relogio = Stopwatch.StartNew();

        var achado = somar(valores);

        var tempo = relogio.Elapsed.TotalMilliseconds;

        Console.WriteLine($"  {nome,12}   {Exato.DistanciaEmUlps(achado, certo),14:n0}"
            + $"   {tempo,9:0.0} ms");
    }

    Console.WriteLine();
    Console.WriteLine("  Eu ia escrever que a soma aos pares e quase de graca, porque ela nao");
    Console.WriteLine("  guarda nada e nao faz conta nenhuma a mais: so muda a ORDEM das");
    Console.WriteLine("  adicoes. A coluna do tempo diz outra coisa. Ela e a mais LENTA das");
    Console.WriteLine("  quatro aqui, porque a recursao quebra a lista e o processador perde o");
    Console.WriteLine("  laco sequencial.");
    Console.WriteLine();
    Console.WriteLine("  E o Neumaier custa o mesmo que a ingenua, apesar de fazer tres contas");
    Console.WriteLine("  a mais por parcela: elas nao dependem umas das outras, entao cabem nos");
    Console.WriteLine("  ciclos que a adicao sequencial desperdica de qualquer jeito. Entre as");
    Console.WriteLine("  quatro, ele e o que nao tem defeito: zero ulps e o tempo da ingenua.");
    Console.WriteLine();
}

static void Custo()
{
    Console.WriteLine();
    Console.WriteLine("  Quanto custa cada resumo, em dez milhoes de valores.");
    Console.WriteLine();

    var sorteio = new Random(29);

    var dados = new double[10_000_000];

    for (var i = 0; i < dados.Length; i++)
    {
        dados[i] = sorteio.NextDouble() * 1_000;
    }

    var relogio = Stopwatch.StartNew();

    var resumo = new Resumo();

    resumo.Juntar(dados);

    var comMomentos = relogio.Elapsed.TotalMilliseconds;

    relogio.Restart();

    Resumo.VarianciaIngenua(dados);

    var ingenua = relogio.Elapsed.TotalMilliseconds;

    relogio.Restart();

    Soma.Ingenua(dados);

    var soSoma = relogio.Elapsed.TotalMilliseconds;

    Console.WriteLine($"    so somar                        {soSoma,9:0.0} ms");
    Console.WriteLine($"    variancia pela soma dos quadr.  {ingenua,9:0.0} ms");
    Console.WriteLine($"    Welford com quatro momentos     {comMomentos,9:0.0} ms");
    Console.WriteLine();
    Console.WriteLine($"    media     {resumo.Media,12:0.000000}");
    Console.WriteLine($"    desvio    {resumo.DesvioPadrao,12:0.000000}");
    Console.WriteLine($"    assimetr. {resumo.Assimetria,12:0.000000}");
    Console.WriteLine($"    curtose   {resumo.Curtose,12:0.000000}");
    Console.WriteLine();
    Console.WriteLine("  A uniforme continua tem assimetria 0 e curtose em excesso -1,2, e e");
    Console.WriteLine("  isso que as duas ultimas linhas tem de mostrar. O Welford com quatro");
    Console.WriteLine("  momentos custa mais que a formula ingenua, e e esse o preco de nao");
    Console.WriteLine("  cancelar: mais contas por valor, uma passada so, e nenhum digito");
    Console.WriteLine("  perdido.");
    Console.WriteLine();
}
