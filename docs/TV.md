# Craquez TV (Google TV / Android TV 4K, ex.: TCL C6K)

A versão para TV é um APK separado (`Craquez-TV-vX.Y.Z.apk`), feito para TVs com Google TV ou Android TV:

- **Pacote próprio** (`com.hellisonvidal.craquez.tv`, nome "Craquez TV"): convive com a versão de celular e se atualiza pelo botão **ATUALIZAR** com o APK de TV.
- **32 e 64 bits** (ARMv7 + ARM64): muitas TVs, inclusive as TCL, rodam o sistema em 32 bits, e um APK só de 64 bits nem instala.
- **Aparece na tela inicial da TV** (banner de TV) e funciona com o controle remoto (setas + OK) ou um controle de videogame Bluetooth.
- **Imagem afinada para a TV**: o campo é desenhado em 1080p e ampliado para o 4K do painel com FSR (nítido e leve para o processador da TV); menus e placar ficam em 4K nativo. Para as partidas, o melhor é o modo de imagem **Jogo** da TV (menor atraso).

## Instalar pelo pen drive

1. **No computador**: baixe o `Craquez-TV-vX.Y.Z.apk` da página de versões do projeto e copie para um pen drive (FAT32 ou exFAT).
2. **Na TV**, instale um gerenciador de arquivos pela Play Store (por exemplo *File Commander* ou *X-plore File Manager*).
3. Libere a instalação de apps desse gerenciador:
   - **Configurações › Sistema › Sobre › Build do Android TV OS**: aperte OK 7 vezes, até aparecer "Você é um desenvolvedor".
   - **Configurações › Apps › Segurança e restrições › Fontes desconhecidas**: ative para o gerenciador de arquivos.
4. Coloque o pen drive na TV, abra o gerenciador, entre no pen drive e selecione o APK › **Instalar**.
5. O Craquez TV aparece em **Apps** (e pode ser fixado na tela inicial).

As próximas versões podem ser instaladas pelo próprio jogo (botão **ATUALIZAR**) ou repetindo os passos com o APK novo, por cima (os dados são mantidos).

## Controle

| Controle remoto | Jogo |
|---|---|
| Setas | mover / navegar nos menus |
| OK | passe / confirmar |
| Voltar | pausa / voltar |

Para jogar de verdade, use um controle Bluetooth (Xbox, PlayStation ou genérico): pareie em **Configurações › Controles remotos e acessórios**.
