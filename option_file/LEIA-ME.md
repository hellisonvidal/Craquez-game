# Option file do Craquez

`option_file.json` traz **tudo o que existe no jogo hoje**, pronto para editar:

| Seção | Conteúdo |
|---|---|
| `ligas` | ligas e a lista de times de cada uma (referência) |
| `teams` | cada time: `name`, `short`, uniformes (`kits`: cores `#RRGGBB`, `pattern`, mangas, acabamento), estádio (`stadium`: nome, capacidade, porte) e elenco (`players`: `index` no elenco, `n` nome, `num`, `pos` GK/DF/MF/FW, `nat`, `ovr` geral 30–99, `traits` características e, opcional, `a` = atributos [velocidade, aceleração, chute, passe, controle, defesa] de 0 a 1); opcional `rating` (força do time, 0–1, usada nas partidas simuladas) |
| `competicoes` | as 55 competições (torneios, ligas e copas nacionais): `nome` e `descricao` editáveis, troféu (`trofeu` 0–14, `cor_trofeu`, `cor_detalhe`); modalidade, formato, participantes e ligas/continentes de origem como referência |
| `bolas` | os 37 modelos de bola (campo e futsal): nome e cores |
| `chuteiras` | os 16 modelos de chuteira: nome e cores (base, detalhe, sola, travas) |
| `locais` | nomes dos 15 estádios, ginásios de futsal, campos de society e ruas |

## Como usar

- **PC (Windows/Linux):** a pasta `option_file` vai junto do jogo, ao lado do executável. Edite
  `option_file.json` e abra o jogo (ou em *Opções › Recarregar option file*). Para usar um
  arquivo em outro lugar, *Opções › Carregar option file…* abre um navegador de pastas.
- **Android:** copie a pasta para a pasta de dados do app
  (`Android/data/<pacote do jogo>/files/option_file/`), ou escolha o arquivo pelo navegador de
  pastas (*Opções › Carregar option file…*, por exemplo em Downloads).
- **Escudos:** PNG quadrado em `crests/`, com o nome indicado em cada time (`"crest"`).
- Apague os times ou as seções que não quiser alterar; o que faltar fica como o original.

O jogo vem com nomes e modelos **originais** (bolas e chuteiras sem marca). No option file você
pode renomear e recolorir como quiser para uso próprio.

## Gerar de novo

No repositório, o arquivo é gerado pelo mesmo código do jogo (`OptionFile.Json()`), com
`tools/ci/Preview.cs optionfile <leagues.json>` (ver o comentário no topo do arquivo).
