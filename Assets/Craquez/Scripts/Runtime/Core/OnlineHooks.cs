using UnityEngine;

namespace Craquez
{
    /// <summary>
    /// Ponte para o módulo on-line (assembly Craquez.Online, que depende dos pacotes da Unity Gaming
    /// Services). O módulo se registra aqui ao abrir o jogo; sem ele, a tela ON-LINE só explica o modo.
    /// </summary>
    public static class OnlineHooks
    {
        /// <summary>Monta o conteúdo da tela ON-LINE (abaixo do título) dentro de <c>area</c>.</summary>
        public static System.Action<App, RectTransform> BuildPage;
        /// <summary>Sai da sala/sessão (chamado ao voltar para o menu).</summary>
        public static System.Action Leave;
        /// <summary>Fim da partida on-line: volta para a sala (true = já pede a revanche).</summary>
        public static System.Action<bool> AfterMatch;
    }
}
