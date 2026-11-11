using System;
using System.Collections.Generic;
using UnityEngine;

namespace Craquez.Sim.Net
{
    /// <summary>
    /// Base do modo on-line (desligado até ficar pronto — ver Online.Enabled): partida em lockstep.
    /// A simulação é determinística (passo fixo de 60 Hz), então cada aparelho roda a sua cópia e só os
    /// comandos dos controles (6 bytes por passo, InputFrame) viajam pela rede, com alguns passos de
    /// atraso para esconder a latência. A cada segundo os dois lados trocam um resumo do estado (hash)
    /// e, se divergirem, a sessão acusa a dessincronização.
    /// </summary>
    public static class Online
    {
        /// <summary>Liga o modo on-line no menu (fica desligado até o resultado estar perfeito).</summary>
        public static bool Enabled = false;
        /// <summary>Motivo de o on-line estar desligado (mostrado na tela do on-line).</summary>
        public static string DisabledReason = "";
        /// <summary>Passos de atraso dos comandos (3 = 50 ms): cobre a ida e volta numa rede boa.</summary>
        public const int DefaultDelay = 3;
    }

    /// <summary>Canal de mensagens entre os dois jogadores (UDP, WebSocket, relay... ou em memória para testes).</summary>
    public interface INetTransport
    {
        bool Connected { get; }
        void Send(byte[] data);
        bool TryReceive(out byte[] data);
    }

    /// <summary>Transporte em memória: dois lados ligados diretamente (testes e partidas locais pela "rede").</summary>
    public class LoopbackTransport : INetTransport
    {
        readonly Queue<byte[]> _in = new Queue<byte[]>();
        LoopbackTransport _peer;
        public bool Connected => _peer != null;

        public static void Pair(out LoopbackTransport a, out LoopbackTransport b)
        {
            a = new LoopbackTransport(); b = new LoopbackTransport();
            a._peer = b; b._peer = a;
        }

        public void Send(byte[] data) { if (_peer != null) _peer._in.Enqueue((byte[])data.Clone()); }
        public bool TryReceive(out byte[] data) { if (_in.Count > 0) { data = _in.Dequeue(); return true; } data = null; return false; }
    }

    /// <summary>
    /// Sessão lockstep de um lado. Mensagens: [tipo 1 byte][passo 4 bytes][dados]. Tipo 1 = comando
    /// (InputFrame), tipo 2 = resumo do estado (hash 4 bytes).
    /// </summary>
    public class LockstepSession
    {
        const byte MsgInput = 1, MsgHash = 2;
        public readonly int localTeam;
        public readonly int delay;
        readonly INetTransport _net;
        readonly Dictionary<int, InputFrame> _local = new Dictionary<int, InputFrame>(), _remote = new Dictionary<int, InputFrame>();
        readonly Dictionary<int, int> _hashLocal = new Dictionary<int, int>(), _hashRemote = new Dictionary<int, int>();
        int _nextSend;
        public int Tick { get; private set; }
        /// <summary>Quantos passos de comando local já foram enviados à frente do passo atual.</summary>
        public int Ahead => _nextSend - Tick;
        /// <summary>O canal com o outro aparelho ainda está de pé?</summary>
        public bool Connected => _net.Connected;
        public bool Desynced { get; private set; }
        public int DesyncTick { get; private set; } = -1;

        public LockstepSession(INetTransport net, int localTeam, int delay = Online.DefaultDelay)
        {
            _net = net;
            this.localTeam = localTeam;
            this.delay = Mathf.Max(0, delay);
            // os primeiros passos (dentro do atraso) andam sem comando
            for (int t = 0; t < this.delay; t++) { _local[t] = default; _remote[t] = default; }
            _nextSend = this.delay;
        }

        /// <summary>Entrega o comando deste aparelho (vale para o passo atual + atraso) e envia ao outro lado.</summary>
        public void PushLocal(InputFrame f)
        {
            f = f.Quantized();
            int t = _nextSend++;
            _local[t] = f;
            var msg = new byte[5 + InputFrame.Size];
            msg[0] = MsgInput;
            WriteInt(msg, 1, t);
            f.Write(msg, 5);
            _net.Send(msg);
        }

        /// <summary>Lê o que chegou da rede.</summary>
        public void Poll()
        {
            while (_net.TryReceive(out var m))
            {
                if (m == null || m.Length < 5) continue;
                int t = ReadInt(m, 1);
                if (m[0] == MsgInput && m.Length >= 5 + InputFrame.Size) _remote[t] = InputFrame.Read(m, 5);
                else if (m[0] == MsgHash && m.Length >= 9) { _hashRemote[t] = ReadInt(m, 5); Check(t); }
            }
        }

        /// <summary>Os dois comandos do passo atual já chegaram?</summary>
        public bool Ready => _local.ContainsKey(Tick) && _remote.ContainsKey(Tick);

        /// <summary>Comandos do passo atual na ordem dos times (0 = mandante, 1 = visitante).</summary>
        public InputFrame[] Frames()
        {
            var a = _local[Tick];
            var b = _remote[Tick];
            return localTeam == 0 ? new[] { a, b } : new[] { b, a };
        }

        /// <summary>Avança a simulação um passo (se os comandos chegaram). Retorna false se precisa esperar a rede.</summary>
        public bool Step(MatchSim sim)
        {
            Poll();
            if (!Ready) return false;
            sim.Step(Frames());
            _local.Remove(Tick);
            _remote.Remove(Tick);
            Tick++;
            if (Tick % 60 == 0)
            {
                int h = sim.StateHash();
                _hashLocal[Tick] = h;
                var msg = new byte[9];
                msg[0] = MsgHash;
                WriteInt(msg, 1, Tick);
                WriteInt(msg, 5, h);
                _net.Send(msg);
                Check(Tick);
            }
            return true;
        }

        void Check(int t)
        {
            if (_hashLocal.TryGetValue(t, out int a) && _hashRemote.TryGetValue(t, out int b))
            {
                if (a != b && !Desynced) { Desynced = true; DesyncTick = t; }
                _hashLocal.Remove(t); _hashRemote.Remove(t);
            }
        }

        static void WriteInt(byte[] b, int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24); }
        static int ReadInt(byte[] b, int o) => b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);
    }
}
