// Prévia em PNG (sem a engine) das bolas e das artes dos menus.
// uso: dotnet preview.dll PASTA   (gera bolas.png e arte_*.png)
using System; using System.IO; using System.IO.Compression; using UnityEngine;
public static class P {
  static void Png(string path, Color32[] px, int w, int h) {
    using var raw = new MemoryStream();
    for (int y = h - 1; y >= 0; y--) { raw.WriteByte(0); for (int x = 0; x < w; x++) { var c = px[y * w + x]; raw.WriteByte(c.r); raw.WriteByte(c.g); raw.WriteByte(c.b); raw.WriteByte(255); } }
    using var z = new MemoryStream();
    z.WriteByte(0x78); z.WriteByte(0x01);
    using (var zs = new DeflateStream(z, System.IO.Compression.CompressionLevel.Optimal, true)) { raw.Position = 0; raw.CopyTo(zs); }
    uint s1 = 1, s2 = 0; foreach (var bt in raw.ToArray()) { s1 = (s1 + bt) % 65521; s2 = (s2 + s1) % 65521; }
    WriteBE(z, (int)((s2 << 16) | s1));
    using var f = File.Create(path);
    void Chunk(string t, byte[] d) { var tb = System.Text.Encoding.ASCII.GetBytes(t); WriteBE(f, d.Length); f.Write(tb); f.Write(d); WriteBE(f, (int)Crc(tb, d)); }
    f.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    var hdr = new byte[13]; BE(hdr, 0, w); BE(hdr, 4, h); hdr[8] = 8; hdr[9] = 6; Chunk("IHDR", hdr);
    Chunk("IDAT", z.ToArray()); Chunk("IEND", new byte[0]);
  }
  static void BE(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
  static void WriteBE(Stream s, int v) { var b = new byte[4]; BE(b, 0, v); s.Write(b); }
  static uint Crc(byte[] a, byte[] b) { uint c = 0xffffffff; foreach (var arr in new[] { a, b }) foreach (var x in arr) { c ^= x; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1; } return c ^ 0xffffffff; }

  // esfera iluminada a partir da textura equiretangular
  static void Sphere(Color32[] dst, int dw, int ox, int oy, int size, Color32[] tex, int tw, float spin) {
    int th = tw / 2; var L = new Vector3(-0.45f, 0.6f, 0.66f).normalized;
    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
      float u = (x + 0.5f) / size * 2 - 1, v = (y + 0.5f) / size * 2 - 1; float r2 = u * u + v * v; if (r2 > 1) continue;
      var n = new Vector3(u, v, Mathf.Sqrt(1 - r2));
      // gira a bola (olhando de frente, um pouco de cima)
      float cs = Mathf.Cos(spin), sn = Mathf.Sin(spin); var q = new Vector3(n.x * cs + n.z * sn, n.y, -n.x * sn + n.z * cs);
      float tilt = 0.35f; var q2 = new Vector3(q.x, q.y * Mathf.Cos(tilt) - q.z * Mathf.Sin(tilt), q.y * Mathf.Sin(tilt) + q.z * Mathf.Cos(tilt));
      float lat = Mathf.Acos(Mathf.Clamp(q2.y, -1, 1)), lon = Mathf.Atan2(q2.z, q2.x); if (lon < 0) lon += Mathf.PI * 2;
      int tx = Mathf.Clamp((int)(lon / (Mathf.PI * 2) * tw), 0, tw - 1), ty = Mathf.Clamp((int)(lat / Mathf.PI * th), 0, th - 1);
      Color c = tex[ty * tw + tx];
      float d = Mathf.Max(0, Vector3.Dot(n, L)); float spec = Mathf.Pow(Mathf.Max(0, Vector3.Dot(Vector3.Reflect(-L, n), new Vector3(0, 0, 1))), 24) * 0.35f;
      c = c * (0.28f + 0.8f * d) + Color.white * spec; c.a = 1;
      dst[(oy + y) * dw + ox + x] = c;
    }
  }
  public static void Main(string[] a) {
    string dir = a[0]; Directory.CreateDirectory(dir);
    int n = Craquez.BallCatalog.All.Length, cell = 200, cols = 8, rows = (n + cols - 1) / cols;
    int W = cols * cell, H = rows * cell; var sheet = new Color32[W * H];
    for (int i = 0; i < sheet.Length; i++) sheet[i] = new Color32(40, 46, 58, 255);
    for (int i = 0; i < n; i++) {
      var tex = Craquez.BallCatalog.Pixels(i, 512);
      int cx = (i % cols) * cell, cy = H - (i / cols + 1) * cell;
      Sphere(sheet, W, cx + 10, cy + 10, cell - 20, tex, 512, 0.6f + i);
      Console.WriteLine(i + " " + Craquez.BallCatalog.All[i].name);
    }
    Png(Path.Combine(dir, "bolas.png"), sheet, W, H);
    if (a.Length > 1 && a[1] == "arte") {
      foreach (Craquez.UI.MenuArt.Kind k in Enum.GetValues(typeof(Craquez.UI.MenuArt.Kind))) {
        int w = 480, h = 600; Png(Path.Combine(dir, "arte_" + k + ".png"), Craquez.UI.MenuArt.TilePixels(k, w, h), w, h);
      }
      for (int v = 0; v < 3; v++) Png(Path.Combine(dir, "local_campo" + v + ".png"), Craquez.UI.MenuArt.VenuePixels(Craquez.Sim.GameModes.Field, v, new Color(0.8f, 0.1f, 0.1f), 640, 360), 640, 360);
    }
  }
}
