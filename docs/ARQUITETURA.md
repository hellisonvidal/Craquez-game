# Arquitetura

## Camadas

```
 Dispositivos (teclado, Xbox, PS, toque) ─┐
 CpuBrain (IA) ───────────────────────────┼─► InputFrame (3 bytes/tick) ─► MatchSim.step() ─► estado
 Rede (futuro: RemoteInput) ──────────────┘                                     │
                                                                                ▼
                                                     MatchView (_draw 2.5D) + Hud + vibração
```

* **`MatchSim`** (`scripts/sim/`) é código puro: não lê `Input`, não usa `randf()`, não depende de nós.
  Roda em passo fixo de 1/60 s. Toda aleatoriedade usa `SimRng` com semente.
* **`InputFrame`** guarda só *estado* (direção + botões segurados). As bordas (apertou/soltou)
  são calculadas dentro da simulação → o quadro é pequeno e idempotente para a rede.
* **`CpuBrain`** gera `InputFrame`s como um jogador humano. A mesma porta que a rede vai usar.
* **`MatchView`** só lê o estado e desenha; pode rodar a qualquer FPS (30–144).

## Física (resumo)

| Elemento | Modelo |
|---|---|
| Bola no ar | gravidade 9,81; arrasto quadrático `k·|v|·v`; Magnus `spin × v` |
| Quique | restituição por superfície (grama 0,58 · madeira 0,45 · asfalto 0,68) e perda horizontal |
| Bola rolando | desaceleração constante (atrito de rolagem) + arrasto leve |
| Traves/travessão | colisão circular com reflexão e restituição |
| Jogador | aceleração/frenagem separadas, penalidade em curvas fechadas em alta velocidade, giro limitado, stamina |
| Condução | a bola é "tocada" à frente do pé (mola amortecida): virar rápido ou correr demais solta a bola |
| Domínio | bola muito rápida para o controle do jogador = desvio em vez de domínio |
| Dividida | probabilidade por atributos e ângulo; por trás pode ser falta |

## Qualidade gráfica

| Preset | Resolução interna | FPS | Extras |
|---|---|---|---|
| Baixa | 854×480 escalado | 30 | sem sombras/torcida/rede, jogador simplificado, rostos 128 px |
| Média | 1280×720 escalado | 60 | sombras, listras; rostos 256 px |
| Alta | nativa | 60 | MSAA 2x, torcida, rastro da bola; rostos 512 px |
| Ultra | nativa (até 4K) | 144 | MSAA 4x; rostos 1024 px |

"Automática" escolhe pelo número de núcleos e plataforma.
