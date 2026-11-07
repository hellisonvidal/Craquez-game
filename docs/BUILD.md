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
