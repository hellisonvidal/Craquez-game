package com.hellisonvidal.craquez;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.media.Image;
import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;
import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.ByteBuffer;

/**
 * Grava o replay em vídeo MP4 (H.264) e salva na galeria (Filmes/Craquez). O jogo entrega cada quadro em
 * RGBA (linhas de baixo para cima, como a Unity lê) com o tempo do quadro; aqui vira YUV 4:2:0 e vai para o
 * codificador do aparelho.
 */
public class CraquezVideo {
    static MediaCodec codec;
    static MediaMuxer muxer;
    static int track = -1, w, h;
    static boolean muxing;
    static File tmp;
    static final MediaCodec.BufferInfo info = new MediaCodec.BufferInfo();

    public static String start(Activity act, int width, int height, int fps) {
        try {
            cancel();
            w = width; h = height;
            tmp = new File(act.getCacheDir(), "replay.mp4");
            if (tmp.exists()) tmp.delete();
            MediaFormat f = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, w, h);
            f.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible);
            f.setInteger(MediaFormat.KEY_BIT_RATE, 6000000);
            f.setInteger(MediaFormat.KEY_FRAME_RATE, fps);
            f.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1);
            codec = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC);
            codec.configure(f, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
            codec.start();
            muxer = new MediaMuxer(tmp.getAbsolutePath(), MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
            track = -1;
            muxing = false;
            return "";
        } catch (Throwable e) {
            cancel();
            return e.getClass().getSimpleName() + ": " + e.getMessage();
        }
    }

    /** Um quadro RGBA (w*h*4, de baixo para cima) no tempo ptsUs. */
    public static String frame(byte[] rgba, long ptsUs) {
        if (codec == null) return "parado";
        try {
            int idx = codec.dequeueInputBuffer(200000);
            if (idx < 0) { drain(false); idx = codec.dequeueInputBuffer(500000); }
            if (idx < 0) return "";   // codificador atrasado: pula o quadro
            Image img = codec.getInputImage(idx);
            Image.Plane[] pl = img.getPlanes();
            ByteBuffer yb = pl[0].getBuffer(), ub = pl[1].getBuffer(), vb = pl[2].getBuffer();
            int yr = pl[0].getRowStride(), yp = pl[0].getPixelStride();
            int ur = pl[1].getRowStride(), up = pl[1].getPixelStride();
            int vr = pl[2].getRowStride(), vp = pl[2].getPixelStride();
            for (int y = 0; y < h; y++) {
                int src = (h - 1 - y) * w * 4;   // vira de cabeça para cima
                int yo = y * yr;
                boolean chromaRow = (y & 1) == 0;
                for (int x = 0; x < w; x++) {
                    int s = src + x * 4;
                    int r = rgba[s] & 255, g = rgba[s + 1] & 255, b = rgba[s + 2] & 255;
                    yb.put(yo + x * yp, (byte) (((66 * r + 129 * g + 25 * b + 128) >> 8) + 16));
                    if (chromaRow && (x & 1) == 0) {
                        int cy = y >> 1, cx = x >> 1;
                        ub.put(cy * ur + cx * up, (byte) (((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128));
                        vb.put(cy * vr + cx * vp, (byte) (((112 * r - 94 * g - 18 * b + 128) >> 8) + 128));
                    }
                }
            }
            codec.queueInputBuffer(idx, 0, w * h * 3 / 2, ptsUs, 0);
            drain(false);
            return "";
        } catch (Throwable e) {
            return e.getClass().getSimpleName() + ": " + e.getMessage();
        }
    }

    static void drain(boolean end) {
        int waits = 0;
        while (true) {
            int o = codec.dequeueOutputBuffer(info, end ? 100000 : 0);
            if (o == MediaCodec.INFO_TRY_AGAIN_LATER) { if (!end || ++waits > 50) return; else continue; }
            if (o == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                track = muxer.addTrack(codec.getOutputFormat());
                muxer.start();
                muxing = true;
                continue;
            }
            if (o < 0) continue;
            ByteBuffer data = codec.getOutputBuffer(o);
            if ((info.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0) info.size = 0;
            if (info.size > 0 && muxing && data != null) {
                data.position(info.offset);
                data.limit(info.offset + info.size);
                muxer.writeSampleData(track, data, info);
            }
            codec.releaseOutputBuffer(o, false);
            if ((info.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) return;
        }
    }

    /** Fecha o vídeo e copia para a galeria. Devolve "ok:<onde>" ou o erro. */
    public static String finish(Activity act, String name, long lastPtsUs) {
        if (codec == null) return "erro:nada gravado";
        try {
            int idx = codec.dequeueInputBuffer(500000);
            if (idx >= 0) codec.queueInputBuffer(idx, 0, 0, lastPtsUs + 33333, MediaCodec.BUFFER_FLAG_END_OF_STREAM);
            drain(true);
            codec.stop(); codec.release(); codec = null;
            if (muxing) muxer.stop();
            muxer.release(); muxer = null;
            if (!muxing) return "erro:o codificador não gerou o vídeo";
            muxing = false;
            String file = name + ".mp4";
            if (Build.VERSION.SDK_INT >= 29) {
                ContentResolver cr = act.getContentResolver();
                ContentValues cv = new ContentValues();
                cv.put(MediaStore.Video.Media.DISPLAY_NAME, file);
                cv.put(MediaStore.Video.Media.MIME_TYPE, "video/mp4");
                cv.put(MediaStore.Video.Media.RELATIVE_PATH, Environment.DIRECTORY_MOVIES + "/Craquez");
                cv.put(MediaStore.Video.Media.IS_PENDING, 1);
                Uri uri = cr.insert(MediaStore.Video.Media.EXTERNAL_CONTENT_URI, cv);
                if (uri == null) return "erro:a galeria recusou o arquivo";
                try (InputStream in = new FileInputStream(tmp); OutputStream out = cr.openOutputStream(uri)) {
                    copy(in, out);
                }
                cv.clear();
                cv.put(MediaStore.Video.Media.IS_PENDING, 0);
                cr.update(uri, cv, null, null);
                tmp.delete();
                return "ok:Filmes/Craquez/" + file;
            }
            File dir = new File(act.getExternalFilesDir(Environment.DIRECTORY_MOVIES), "");
            dir.mkdirs();
            File dst = new File(dir, file);
            try (InputStream in = new FileInputStream(tmp); OutputStream out = new java.io.FileOutputStream(dst)) {
                copy(in, out);
            }
            tmp.delete();
            return "ok:" + dst.getAbsolutePath();
        } catch (Throwable e) {
            cancel();
            return "erro:" + e.getClass().getSimpleName() + ": " + e.getMessage();
        }
    }

    static void copy(InputStream in, OutputStream out) throws java.io.IOException {
        byte[] buf = new byte[1 << 16];
        int n;
        while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
    }

    public static void cancel() {
        try { if (codec != null) { codec.stop(); codec.release(); } } catch (Throwable ignored) { }
        try { if (muxer != null) { if (muxing) muxer.stop(); muxer.release(); } } catch (Throwable ignored) { }
        codec = null; muxer = null; muxing = false; track = -1;
    }
}
