using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Craquez.Sim.Net;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Craquez.Online
{
    /// <summary>
    /// Canal da partida on-line: Unity Transport passando pelo Relay da Unity (os aparelhos ficam atrás de
    /// roteador/operadora e não se conectam direto). Entrega garantida e em ordem (o lockstep não pode perder
    /// comando). As mensagens pequenas de cada quadro vão juntas num pacote: [tamanho 2 bytes][mensagem]...
    /// Também é o "network handler" da sessão: a sessão da Unity entrega aqui os dados do Relay ao começar.
    /// </summary>
    public class RelayLink : INetTransport, INetworkHandler, IDisposable
    {
        NetworkDriver _driver;
        NetworkPipeline _pipe;
        NetworkConnection _conn;
        bool _host, _started;
        readonly Queue<byte[]> _in = new Queue<byte[]>();
        readonly List<byte[]> _out = new List<byte[]>();
        const int MaxPacket = 1000;
        const int MaxQueued = 8192;

        /// <summary>Conectado ao outro aparelho (o anfitrião, quando o convidado entra).</summary>
        public bool Connected { get; private set; }
        /// <summary>Já esteve conectado alguma vez (para distinguir "esperando" de "caiu").</summary>
        public bool WasConnected { get; private set; }
        public bool IsHost => _host;
        public string Error { get; private set; }

        // ------------------------------------------------------------ sessão (INetworkHandler)
        public Task StartAsync(NetworkConfiguration configuration)
        {
            try
            {
                _host = configuration.Role != NetworkRole.Client;
                var data = configuration.RelayServerData;
                var settings = new NetworkSettings(Unity.Collections.Allocator.Temp);
                settings.WithRelayParameters(ref data);
                settings.WithReliableStageParameters(windowSize: 64);
                _driver = NetworkDriver.Create(settings);
                _pipe = _driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));
                if (_driver.Bind(NetworkEndpoint.AnyIpv4) != 0) throw new Exception("não deu para abrir a porta de rede");
                if (_host)
                {
                    if (_driver.Listen() != 0) throw new Exception("não deu para receber conexões");
                }
                else _conn = _driver.Connect(data.Endpoint);
                _started = true;
            }
            catch (Exception e)
            {
                Error = e.Message;
                Debug.LogWarning("[on-line] transporte: " + e);
                throw;
            }
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }

        // ------------------------------------------------------------ INetTransport
        public void Send(byte[] data)
        {
            if (data == null || data.Length == 0 || data.Length > MaxPacket - 2) return;
            _out.Add(data);
        }

        public bool TryReceive(out byte[] data)
        {
            if (_in.Count > 0) { data = _in.Dequeue(); return true; }
            data = null;
            return false;
        }

        /// <summary>Roda a rede (chamado a cada quadro e antes de cada passo da partida).</summary>
        public void Pump()
        {
            if (!_started || !_driver.IsCreated) return;
            _driver.ScheduleUpdate().Complete();
            if (_host)
            {
                NetworkConnection c;
                while ((c = _driver.Accept()).IsCreated)
                {
                    if (!_conn.IsCreated) { _conn = c; Connected = true; WasConnected = true; }   // (do lado de quem aceita não vem evento Connect)
                    else _driver.Disconnect(c);   // sala de dois: um terceiro não entra
                }
            }
            if (!_conn.IsCreated) return;
            NetworkEvent.Type ev;
            while ((ev = _driver.PopEventForConnection(_conn, out var reader)) != NetworkEvent.Type.Empty)
            {
                switch (ev)
                {
                    case NetworkEvent.Type.Connect:
                        Connected = true;
                        WasConnected = true;
                        break;
                    case NetworkEvent.Type.Data:
                    {
                        var buf = new byte[reader.Length];
                        reader.ReadBytes(new Span<byte>(buf));
                        int o = 0;
                        while (o + 2 <= buf.Length)
                        {
                            int n = buf[o] | (buf[o + 1] << 8);
                            o += 2;
                            if (n <= 0 || o + n > buf.Length) break;
                            var m = new byte[n];
                            Buffer.BlockCopy(buf, o, m, 0, n);
                            // (fila limitada: um outro lado inundando mensagens não esgota a memória)
                            if (_in.Count < MaxQueued) _in.Enqueue(m);
                            o += n;
                        }
                        break;
                    }
                    case NetworkEvent.Type.Disconnect:
                        Connected = false;
                        _conn = default;
                        return;
                }
            }
            Flush();
        }

        void Flush()
        {
            if (!Connected || _out.Count == 0) return;
            int i = 0;
            while (i < _out.Count)
            {
                // junta as mensagens que couberem num pacote
                int size = 0, j = i;
                while (j < _out.Count && size + 2 + _out[j].Length <= MaxPacket) { size += 2 + _out[j].Length; j++; }
                var pkt = new byte[size];
                int o = 0;
                for (int k = i; k < j; k++)
                {
                    var m = _out[k];
                    pkt[o] = (byte)m.Length; pkt[o + 1] = (byte)(m.Length >> 8);
                    Buffer.BlockCopy(m, 0, pkt, o + 2, m.Length);
                    o += 2 + m.Length;
                }
                if (_driver.BeginSend(_pipe, _conn, out var w) != 0) break;   // janela cheia: tenta no próximo quadro
                w.WriteBytes(new Span<byte>(pkt));
                if (_driver.EndSend(w) < 0) break;
                i = j;
            }
            _out.RemoveRange(0, i);
        }

        public void Dispose()
        {
            _started = false;
            Connected = false;
            if (_driver.IsCreated)
            {
                try { if (_conn.IsCreated) { _driver.Disconnect(_conn); _driver.ScheduleUpdate().Complete(); } } catch { }
                _driver.Dispose();
            }
            _conn = default;
            _in.Clear();
            _out.Clear();
        }
    }
}
