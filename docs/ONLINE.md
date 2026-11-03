# Preparação para PvP online

O que **já existe**:

1. **Simulação determinística** em passo fixo — verificado por teste (`tests/run_tests.gd`, "Determinismo").
2. **Entrada mínima**: `InputFrame` = 3 bytes/tick (≈ 180 B/s por jogador a 60 Hz).
3. **`NetSession`** (`scripts/net/net_session.gd`): contrato que a partida usa; hoje `LocalSession`.
4. **`ReplayBuffer`**: grava entradas por tick → replays e reprodução de dessincronias.
5. **`StateHash`**: hash do estado para checar sincronia a cada N ticks.

Plano de implementação:

1. `EnetSession` (Godot `ENetMultiplayerPeer`, UDP) para LAN/IP direto; depois WebRTC ou relay para NAT.
2. **Atraso de entrada** de 2–3 ticks + **rollback** (salvar/restaurar `MatchSim` — adicionar `snapshot()`/`restore()`),
   re-simulando quando a entrada real do adversário difere da prevista (repetir a última).
3. Trocar `float` por ponto fixo **se** for jogar entre arquiteturas diferentes (PC x Android ARM):
   floats IEEE costumam bater na mesma build, mas não é garantido entre CPUs/compiladores.
   Alternativa mais simples: servidor autoritativo (host) + correção de estado a cada ~100 ms.
4. Matchmaking: começar com "código de sala"; depois serviço dedicado.
