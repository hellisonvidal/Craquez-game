# PvP online

## Como jogar (v0.2)

1. Os dois abrem o Craquez. Quem vai **hospedar** escolhe modo/times/tempo e clica em **Criar sala** — o menu mostra o IP local.
2. O outro digita esse IP e clica em **Entrar**. A partida começa sozinha nas duas telas.
3. Mesma rede Wi-Fi/LAN funciona direto. Pela internet, o anfitrião precisa liberar/redirecionar a **porta 27015 UDP**
   no roteador (ou usar uma VPN de jogos tipo ZeroTier/Tailscale/Radmin e o IP dela).

Anfitrião = time da casa (P1), convidado = visitante. Cada um usa o controle/teclado/toque escolhido em "Jogador 1".

## Como funciona

* **Lockstep com atraso de entrada** (`scripts/net/lockstep.gd`): a entrada capturada agora vale para 3 ticks no futuro
  (≈ 50 ms). A simulação só avança um tick quando tem as entradas dos dois lados → as duas máquinas simulam a mesma partida.
* **Rede** (`scripts/net/enet_session.gd`): ENet/UDP. Cada pacote de entradas leva as últimas 8 (redundância), então perder
  pacotes não trava; se travar esperando, reenvia.
* **Banda**: ~3 bytes × 60 ticks × redundância ≈ 2–3 KB/s. Funciona até em 3G.
* **Anti-dessincronia**: a cada 60 ticks os dois trocam um hash do estado; divergência aparece na tela.
* Testado no CI: duas sessões ENet reais em localhost jogam 600 ticks CPU×CPU e terminam com estado idêntico.

## Limitações e próximos passos

1. **Latência alta** (> 80 ms) deixa os controles "pesados", porque o lockstep espera. Próximo passo: **rollback**
   (prever a entrada do adversário, simular adiante e corrigir) — precisa de `MatchSim.snapshot()/restore()`.
2. **PC × Android**: a simulação usa `float`. Na mesma build costuma bater, mas entre CPUs diferentes (x86 × ARM) não é
   garantido. Se aparecer "Dessincronia" nesse cenário, a solução é ponto fixo na simulação ou servidor autoritativo.
3. **Matchmaking / NAT**: hoje é IP direto. Próximo: servidor de "código de sala" com relay, ou WebRTC.
4. Pausa online não para a partida (só abre o menu localmente).
