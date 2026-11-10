# Modo on-line: o que falta e o que você precisa fazer

## O que já está pronto (no jogo)

- **Partida em lockstep**: cada aparelho roda a mesma simulação (ela é determinística, a 60 passos por segundo) e só os comandos dos controles viajam pela rede — 6 bytes por passo, com alguns passos de atraso para esconder a latência. A cada segundo os dois lados comparam um resumo do estado da partida e, se divergirem, o jogo percebe.
- **Teste automático**: no CI, duas partidas ligadas por uma rede simulada rodam 40 s e terminam idênticas.
- **Menu**: o cartão **ON-LINE** está no menu principal (por enquanto mostra "em breve").

## O que falta

Um **serviço que coloque os dois aparelhos em contato** pela internet (celulares não conseguem se conectar direto um ao outro: estão atrás do roteador/operadora). Esse serviço tem duas partes:

1. **Encontro (lobby)**: um jogador cria a sala (com um código) ou entra na fila da partida rápida; o outro entra.
2. **Retransmissão (relay)**: um servidor que repassa os pacotes de um aparelho para o outro.

## Caminho recomendado: Unity Gaming Services (grátis no começo)

A própria Unity oferece os dois serviços (**Lobby** e **Relay**) com cota gratuita mensal que cobre muitos jogos de teste. É o caminho com menos trabalho para você — o código eu faço.

### Passo a passo (só você pode fazer — exige a sua conta)

1. Entre em **https://cloud.unity.com** com a mesma conta Unity usada na licença do CI.
2. **Create project** (ou "Projects › New"), nome **Craquez**.
3. No projeto, abra **Products** (ou "Explore services") e ative:
   - **Authentication** (login anônimo dos jogadores — sem cadastro),
   - **Lobby**,
   - **Relay**.
   (Os três têm plano gratuito; a Unity pode pedir um cartão para cobrança só acima da cota.)
4. Em **Project settings**, copie o **Project ID** (um código como `a1b2c3d4-...`).
5. Me mande esse **Project ID** (não é segredo — é só o identificador do projeto).

### O que eu faço depois

- Instalo os pacotes `com.unity.services.authentication`, `com.unity.services.lobby` e `com.unity.services.relay`.
- Ligo o transporte do Relay na sessão lockstep que já existe.
- Faço as telas: **Partida rápida**, **Criar sala** (mostra o código), **Entrar com código**, e a sala de espera com os dois times.
- Ativo o cartão ON-LINE (`Online.Enabled = true`) quando o teste entre dois aparelhos de verdade estiver perfeito.

### Como vamos testar

1. Eu publico uma versão de teste.
2. Você instala em **dois aparelhos** (pode ser celular + TV, ou dois celulares).
3. Um cria a sala, o outro entra com o código, e vocês jogam alguns minutos.
4. Se aparecer qualquer diferença entre as duas telas (bola em lugares diferentes), o jogo registra o momento — e eu corrijo.

> Ponto de atenção: processadores diferentes podem fazer contas com casas decimais levemente
> diferentes. O teste entre aparelhos reais é o que confirma que a partida fica idêntica nos dois.

## Alternativa (mais trabalho, sem depender da Unity)

Um pequeno servidor próprio (WebSocket) num serviço de hospedagem (ex.: Fly.io ou Render, com plano gratuito). Você precisaria criar a conta nesse serviço e me passar o acesso para eu publicar o servidor. Só vale a pena se a Unity não for uma opção.
