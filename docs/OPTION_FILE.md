# Option file (nomes, uniformes, elencos e escudos)

O Craquez vem com clubes de nomes alterados e jogadores fictícios. Como nos
"option files" do PES, você pode personalizar tudo **no seu próprio computador**.

## Onde fica

O jogo procura `option_file/option_file.json` em:

1. **Windows:** pasta `option_file` ao lado do `Craquez.exe`.
2. Pasta de dados do jogo (`user://option_file`), que também vale no Android.
   No Windows ela fica em `%APPDATA%\Godot\app_userdata\Craquez\option_file`.

## Como começar

1. No jogo: **Modos de jogo → Opções → Exportar modelo do option file**.
2. Abra o `option_file_modelo.json` gerado. Ele traz **todos os times** com os valores atuais.
3. Edite o que quiser e salve como `option_file.json` na mesma pasta.
4. Escudos: coloque imagens PNG quadradas (ex.: 256×256) em `crests/`, com o nome
   indicado no campo `crest` de cada time.
5. Volte em **Opções → Recarregar**.

## Formato

```json
{
  "teams": {
    "bra/RNC": {
      "name": "Nome do clube",
      "short": "ABC",
      "kits": [
        {"c1": "#c8102e", "c2": "#111827", "shorts": "#111827", "socks": "#c8102e", "pattern": "listras"},
        {"c1": "#f8fafc", "c2": "#c8102e", "shorts": "#f8fafc", "socks": "#f8fafc", "pattern": "liso"},
        {"c1": "#111827", "c2": "#c8102e", "shorts": "#111827", "socks": "#111827", "pattern": "faixa"}
      ],
      "stadium": {"name": "Nome do estádio", "cap": 78000},
      "crest": "crests/bra_RNC.png",
      "players": [{"index": 0, "n": "Nome do goleiro", "num": 1}]
    }
  }
}
```

- A chave de cada time é `liga/SIGLA_ORIGINAL`, conforme o modelo exportado.
- Uniformes: `pattern` aceita `liso`, `listras`, `aros`, `faixa`, `metades`, `riscas` e `faixa_peito`.
- Jogadores: `index` é a posição no elenco (veja o modelo). Os campos que você
  omitir continuam como estão.
- Apague os times que não quiser alterar.

O conteúdo que você colocar no seu option file é de sua responsabilidade e fica só
no seu aparelho. Ele não é distribuído com o jogo.
