# estatistica

Soma, variância e correlação do zero em C# e .NET 8, com aritmética exata em
inteiro grande como juiz do ponto flutuante.

```
$ dotnet medidor.dll cancelamento

    deslocamento      soma dos quadrados          Welford
  --------------------------------------------------------------
    10^7                             30               30
    10^8                29.333333333333               30
    10^9              -170.666666666667               30
    10^10              21845.3333333333               30
    10^11                             0               30
```

## O juiz não é outro ponto flutuante

Testar ponto flutuante contra ponto flutuante é andar em círculo: a referência
tem o mesmo erro do que se quer medir.

Aqui a referência é outra coisa. Todo `double` é exatamente uma fração com
denominador potência de dois, por definição do IEEE 754. Então a soma de uma
lista deles tem um valor exato, que cabe num `BigInteger`, e esse valor
arredondado uma vez só no fim é o que a soma **deveria** ter dado. Qualquer
algoritmo de somar pode ser medido contra ele, em qualquer entrada, sem tabela
nem tolerância inventada.

A unidade também não é porcentagem. É quantos `double` representáveis existem
entre o achado e o certo, o que a literatura chama de casas na última posição.
Perto do zero toda diferença relativa vira enorme e perto do infinito toda
diferença vira desprezível; contar representáveis funciona em qualquer escala.

São 17 testes.

## O Kahan erra, e bastam três números

A soma compensada de Kahan é a resposta padrão para erro de arredondamento. Ela
tem um buraco que não é teórico: quando a parcela nova é **maior** que o
acumulado, é a parcela que perde bits, e a compensação olha para o lado errado.

```
soma de [1e16, 1, -1e16]

  exata        1
  ingênua      0
  Kahan        0
  Neumaier     1
```

A variante de Neumaier faz uma comparação a mais e fecha o buraco. Sobre dez mil
listas sorteadas de tamanho e escala variados, ela é a única que fica em zero ou
uma casa em **todas**:

```
$ dotnet medidor.dll somas

     algoritmo      erro em ulps        tempo
  --------------------------------------------------
       ingenua              546         0.8 ms
         Kahan                0         2.2 ms
      Neumaier                0         0.8 ms
     aos pares                2         3.1 ms
         exata                0        97.2 ms
```

Eu ia escrever que a soma aos pares é quase de graça, porque ela não guarda nada
e não faz conta nenhuma a mais: só muda a ordem das adições. A coluna do tempo diz
outra coisa, e ela é a mais lenta das quatro, porque a recursão quebra a lista e o
laço sequencial se perde.

O Neumaier custa o mesmo que a ingênua apesar de fazer três contas a mais por
parcela, porque elas não dependem umas das outras e cabem nos ciclos que a adição
sequencial desperdiça de qualquer jeito.

## A variância que fica negativa

A fórmula de uma passada que todo mundo aprende é
`Var = (soma dos quadrados)/n - media²`. Ela é algebricamente correta e
numericamente desastrosa: os dois termos são enormes e quase iguais, e a
subtração entre eles cancela os dígitos que importam.

Com os mesmos quatro números, 4, 7, 13 e 16, somados a um deslocamento que cresce,
ela perde um dígito por potência de dez. Em `10⁹` ela devolve **variância
negativa**, que é impossível por definição, e em `10¹¹` ela devolve zero, que é o
jeito mais silencioso de errar: nenhuma exceção, nenhum sinal estranho, só a
afirmação de que os quatro números são iguais.

O método de Welford, de 1962, atualiza a média e a soma dos desvios ao quadrado
direto, sem nunca formar aqueles dois números. Dá 30 em todas as linhas.

Eu tinha escrito o teste afirmando que em `10⁸` a fórmula ingênua daria negativo.
Deu 29,333: errada, e ainda positiva. O sinal vira uma casa decimal depois, e
quem mostrou foi a ferramenta.

## Nenhum algoritmo é melhor que a condição do problema

```
$ dotnet medidor.dll condicionamento

        media   desvio   condicao     Welford    soma dos quadrados
  ------------------------------------------------------------------------
       1.1e+1    579.9     1.0e+0     3.5e-16                0.0e+0
       9.9e+2    569.0     2.0e+0     3.6e-16               1.8e-16
       1.0e+6    579.1     1.7e+3     2.6e-14               3.0e-10
       1.0e+9    578.6     1.7e+6     3.0e-11                2.9e-3
      1.0e+12    587.6     1.7e+9      1.6e-9                8.0e+2
```

A primeira versão do teste da variância exigia erro menor que 64 casas e falhou
com 531 no primeiro caso. A culpa não era do Welford: era do limite, que eu
tinha escolhido sem olhar o condicionamento.

O condicionamento da variância é mais ou menos a razão entre a média e o desvio
padrão. Dados longe da origem e pouco espalhados são mal condicionados, e nenhum
algoritmo escapa disso. A tabela mostra os dois expoentes: o erro do Welford
cresce com a condição e o da soma dos quadrados cresce com o **quadrado** dela,
que é o resultado de Chan, Golub e LeVeque de 1983.

Então o teste não exige um número fixo. Ele exige que o erro fique abaixo de um
múltiplo da condição, que é o que o problema permite.

## O quarteto de Anscombe

Quatro conjuntos publicados por Francis Anscombe em 1973, com a mesma média em
`x` e em `y`, a mesma variância nos dois, a mesma correlação e a mesma reta de
regressão. Um é linear com ruído, um é uma parábola, um é uma reta perfeita com um
ponto fora, e um é uma coluna de dez pontos idênticos mais um ponto solitário que
sozinho define a inclinação.

Em `x` os quatro batem exato: média 9 e variância 11. Em `y` eles não batem
exato, e dizer que batem seria copiar a lenda em vez de conferir o dado. A
variância de `y` vai de 4,12262 a 4,12763 entre os quatro. Cinco milésimos é o
que separa conjuntos que não têm nada a ver um com o outro, e é por isso que o
teste confere a **diferença** entre eles em vez de um valor publicado.

Tire o ponto solitário do quarto conjunto e a biblioteca se recusa a traçar a
reta, porque `x` passa a ser constante. A inclinação inteira vinha de um ponto.

## Invariantes em vez de implementação de referência

Para correlação e regressão não há juiz externo, e não precisa haver. A resposta
certa tem propriedades que a errada não tem:

A correlação não muda quando as unidades mudam, então medir em metros ou em
polegadas dá o mesmo valor. Os resíduos da reta somam zero e são ortogonais ao
previsor, que é a própria definição de mínimos quadrados escrita de outro jeito.
A reta passa pelo centro de massa dos pontos. O `R²` é o quadrado do Pearson, e
também é a fração da variância que a reta explica.

Cada uma dessas é conferida em milhares de conjuntos sorteados. Conferir uma
consequência da definição é conferir a conta sem repetir a conta.

E tem o limite do Pearson, que é a razão de ele ser mal usado: os 21 pontos de
`-10` a `10` sobre a parábola `y = x²` têm relação **determinística**, sem ruído
nenhum, e a correlação deles é exatamente zero.

## Rodar

.NET 8. Zero dependências fora do xUnit, e só nos testes.

```
dotnet test testes/Estatistica.Testes/Estatistica.Testes.csproj -c Release
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- cancelamento
```

## O que ele não faz

Não tem mediana nem quantis. Quantil exato pede a amostra inteira ordenada, e
quantil aproximado em fluxo é outro projeto, com outra discussão.

Não tem teste de hipótese, intervalo de confiança nem valor-p. São contas fáceis
e decisões difíceis, e a parte difícil não cabe numa biblioteca.

Não tem regressão múltipla. Duas variáveis já pedem decomposição QR e um tipo de
matriz, e aí o assunto deixa de ser estatística e passa a ser álgebra linear
numérica.

Não tem distribuições, nem geração de amostras delas.

A soma exata é correta na faixa normal. Com resultado subnormal pode haver
arredondamento duplo, e essa faixa não é exercitada pelos testes.

## Licença

MIT.
