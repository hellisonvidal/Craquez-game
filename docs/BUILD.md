# Como compilar o Craquez (Unity 6.3 LTS)

O projeto é **Unity 6000.3.28f1** com **URP**. Plataformas: **Android** (APK, IL2CPP ARM64), **Windows** (x64) e **Linux** (x86-64, Vulkan com OpenGL de reserva).

## Pelo GitHub (automático)

Cada envio ao repositório roda o workflow `CI`:

| Job | Precisa de licença? | O que faz |
|---|---|---|
| Compilar e testar | não | compila todo o C# contra o editor e roda os testes da simulação |
| Testes no editor Unity | sim | roda os testes EditMode dentro do Unity |
| Build Android / Windows / Linux | sim | gera `Craquez.apk`, `Craquez.exe` (+ instalador) e `Craquez-Linux.tar.gz`, disponíveis em **Actions → execução → Artifacts** |

### Configurar a licença (uma vez só)

A licença **Unity Personal é gratuita** (até US$ 200 mil de receita por ano).

1. Instale o **Unity Hub** no seu PC, entre com sua conta Unity e ative a licença Personal
   (Hub → Preferências → Licenças → Adicionar → Personal).
2. Pegue o arquivo da licença:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
3. No GitHub: **Settings → Secrets and variables → Actions → New repository secret** e crie:
   - `UNITY_LICENSE`: o conteúdo inteiro do arquivo `Unity_lic.ulf`
   - `UNITY_EMAIL`: o e-mail da conta Unity
   - `UNITY_PASSWORD`: a senha da conta Unity
4. Rode o workflow de novo (Actions → CI → Re-run).

Os segredos ficam criptografados no GitHub e só são usados pelos jobs de build.

### Chave de assinatura do Android (para atualizar pelo jogo)

Sem uma chave fixa, cada build sai assinado com uma chave de depuração nova e o Android recusa
instalar a versão nova por cima da antiga ("app não instalado"). Crie a chave uma vez, no seu PC
(precisa do Java; troque `SUA_SENHA`), e guarde o arquivo `craquez.keystore` num lugar seguro:

```bash
keytool -genkeypair -v -keystore craquez.keystore -alias craquez -keyalg RSA -keysize 2048 -validity 36500 -storepass SUA_SENHA -keypass SUA_SENHA -dname "CN=Craquez"
```

Depois crie quatro segredos em **Settings → Secrets and variables → Actions**:

- `ANDROID_KEYSTORE_BASE64`: o arquivo em base64 (`base64 -w0 craquez.keystore` no Linux,
  `[Convert]::ToBase64String([IO.File]::ReadAllBytes("craquez.keystore"))` no PowerShell)
- `ANDROID_KEYSTORE_PASS` e `ANDROID_KEYALIAS_PASS`: a senha
- `ANDROID_KEYALIAS_NAME`: `craquez`

Ou pelo terminal, com o `gh` instalado:

```bash
base64 -w0 craquez.keystore | gh secret set ANDROID_KEYSTORE_BASE64 --repo hellisonvidal/Craquez-game
```

```bash
gh secret set ANDROID_KEYSTORE_PASS --repo hellisonvidal/Craquez-game --body SUA_SENHA
```

```bash
gh secret set ANDROID_KEYALIAS_PASS --repo hellisonvidal/Craquez-game --body SUA_SENHA
```

```bash
gh secret set ANDROID_KEYALIAS_NAME --repo hellisonvidal/Craquez-game --body craquez
```

A primeira versão assinada com essa chave precisa ser instalada desinstalando a antiga uma vez;
daí em diante o botão **ATUALIZAR** do jogo baixa o APK novo e abre o instalador do Android.

## No seu PC

1. Abra a pasta do repositório no Unity Hub (**Add project from disk**) com o editor 6000.3.28f1
   e os módulos **Android Build Support** e **Windows Build Support**.
2. Menu **Craquez → Build Android (APK)**, **Craquez → Build Windows** ou **Craquez → Build Linux** (módulo **Linux Build Support (Mono/IL2CPP)**).
   Saída em `Builds/`.

## Linux

Extraia o `Craquez-Linux.tar.gz`, entre na pasta `Craquez` e rode `./Craquez`.
Para ter o atalho no menu de aplicativos: `./instalar.sh`.

## Assets gerados

- Atletas e animações: `tools/blender/` (Blender 5.2 como módulo Python + MPFB/MakeHuman).
  - `build_athletes.py` gera os 4 biotipos com uniforme, rosto variável e 5 cabelos.
  - `retarget.py` transfere a captura de movimento CMU para o esqueleto dos atletas.
- Ligas e elencos: `tools/league_gen.py` → `Assets/Craquez/Resources/Data/leagues.json`.
- Bandeiras: `tools/flags.py` (pacote flag-icons, MIT) → `Assets/Craquez/Resources/Flags/`.
- Uniformes: `tools/kits_clubes.py` e `tools/kits_selecoes.py` → `leagues.json`.
- Materiais-base URP: gerados automaticamente no build (`Craquez → Gerar materiais`).
