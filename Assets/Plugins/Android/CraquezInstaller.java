package com.hellisonvidal.craquez;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageInfo;
import android.content.pm.PackageInstaller;
import android.content.pm.PackageManager;
import android.content.pm.Signature;
import android.os.Build;
import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.Arrays;

/**
 * Atualização por dentro do jogo com o instalador de sessões do Android (PackageInstaller), no lugar de
 * abrir o APK com ACTION_VIEW: no Android 14+ (Samsung One UI) o caminho antigo falhava com um genérico
 * "app não instalado". Aqui o APK é conferido antes (pacote, versão e assinatura iguais à instalada) e o
 * resultado da instalação volta com o motivo exato, que o jogo lê em {@link #status()}.
 */
public class CraquezInstaller {
    static final String ACTION = ".CRAQUEZ_INSTALL_STATUS";

    /** "" ainda nada; "wait" aguardando o usuário/sistema; "ok"; ou "erro:<código>:<mensagem>". */
    static volatile String state = "";
    static BroadcastReceiver receiver;

    public static String status() { return state; }

    /** Confere o APK baixado contra o app instalado. Devolve "" se pode instalar, senão o motivo. */
    public static String check(Activity act, String path) {
        try {
            PackageManager pm = act.getPackageManager();
            File f = new File(path);
            if (!f.exists() || f.length() < 1024) return "arquivo:o download não foi salvo";
            int flags = Build.VERSION.SDK_INT >= 28 ? PackageManager.GET_SIGNING_CERTIFICATES : PackageManager.GET_SIGNATURES;
            PackageInfo nova = pm.getPackageArchiveInfo(path, flags);
            if (nova == null) return "arquivo:o APK baixado está corrompido";
            String pkg = act.getPackageName();
            if (!pkg.equals(nova.packageName)) return "pacote:o APK é de outro aplicativo (" + nova.packageName + ")";
            PackageInfo atual = pm.getPackageInfo(pkg, flags);
            if (code(nova) < code(atual)) return "versao:o APK é mais antigo que o instalado";
            if (!sameSigner(sig(atual), sig(nova))) return "assinatura:a versão instalada foi assinada com outra chave";
        } catch (Throwable e) {
            return "";   // se a conferência falhar, deixa o instalador decidir
        }
        return "";
    }

    static long code(PackageInfo p) {
        return Build.VERSION.SDK_INT >= 28 ? p.getLongVersionCode() : p.versionCode;
    }

    @SuppressWarnings("deprecation")
    static Signature[] sig(PackageInfo p) {
        if (Build.VERSION.SDK_INT >= 28 && p.signingInfo != null)
            return p.signingInfo.hasMultipleSigners() ? p.signingInfo.getApkContentsSigners() : p.signingInfo.getSigningCertificateHistory();
        return p.signatures;
    }

    static boolean sameSigner(Signature[] a, Signature[] b) {
        if (a == null || b == null || a.length == 0 || b.length == 0) return true;   // sem dados: não bloqueia
        for (Signature x : a)
            for (Signature y : b)
                if (Arrays.equals(x.toByteArray(), y.toByteArray())) return true;
        return false;
    }

    /** Copia o APK para uma sessão de instalação e a confirma. O resultado chega em {@link #status()}. */
    public static String install(final Activity act, String path) {
        state = "wait";
        PackageInstaller.Session session = null;
        try {
            final Context app = act.getApplicationContext();
            final String action = app.getPackageName() + ACTION;
            if (receiver == null) {
                receiver = new BroadcastReceiver() {
                    @Override public void onReceive(Context c, Intent i) {
                        int st = i.getIntExtra(PackageInstaller.EXTRA_STATUS, PackageInstaller.STATUS_FAILURE);
                        if (st == PackageInstaller.STATUS_PENDING_USER_ACTION) {
                            Intent confirm = i.getParcelableExtra(Intent.EXTRA_INTENT);
                            if (confirm != null) {
                                confirm.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                                try { act.startActivity(confirm); } catch (Throwable e) { app.startActivity(confirm); }
                            }
                            state = "wait";
                        } else if (st == PackageInstaller.STATUS_SUCCESS) {
                            state = "ok";
                        } else {
                            String m = i.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE);
                            state = "erro:" + st + ":" + (m == null ? "" : m);
                        }
                    }
                };
                IntentFilter filter = new IntentFilter(action);
                if (Build.VERSION.SDK_INT >= 33) app.registerReceiver(receiver, filter, Context.RECEIVER_NOT_EXPORTED);
                else app.registerReceiver(receiver, filter);
            }

            File f = new File(path);
            PackageInstaller pi = app.getPackageManager().getPackageInstaller();
            PackageInstaller.SessionParams params = new PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL);
            params.setAppPackageName(app.getPackageName());
            params.setSize(f.length());
            if (Build.VERSION.SDK_INT >= 31) params.setRequireUserAction(PackageInstaller.SessionParams.USER_ACTION_NOT_REQUIRED);
            int id = pi.createSession(params);
            session = pi.openSession(id);
            try (InputStream in = new FileInputStream(f); OutputStream out = session.openWrite("craquez.apk", 0, f.length())) {
                byte[] buf = new byte[1 << 16];
                int n;
                while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
                session.fsync(out);
            }
            Intent cb = new Intent(action).setPackage(app.getPackageName());
            int pf = PendingIntent.FLAG_UPDATE_CURRENT | (Build.VERSION.SDK_INT >= 31 ? PendingIntent.FLAG_MUTABLE : 0);
            PendingIntent pend = PendingIntent.getBroadcast(app, id, cb, pf);
            session.commit(pend.getIntentSender());
            session.close();
            return "";
        } catch (Throwable e) {
            if (session != null) try { session.abandon(); } catch (Throwable ignored) { }
            state = "erro:-1:" + e.getMessage();
            return e.getClass().getSimpleName() + ": " + e.getMessage();
        }
    }
}
