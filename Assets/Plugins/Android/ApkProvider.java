package com.hellisonvidal.craquez;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import java.io.File;
import java.io.FileNotFoundException;

/**
 * Entrega ao instalador do Android o APK da versão nova, baixado pelo jogo em cache/update/craquez.apk
 * (só leitura, só para quem recebeu a permissão da intenção de instalar).
 */
public class ApkProvider extends ContentProvider {
    private File file() {
        return new File(new File(getContext().getCacheDir(), "update"), "craquez.apk");
    }

    @Override public boolean onCreate() { return true; }

    @Override public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        return ParcelFileDescriptor.open(file(), ParcelFileDescriptor.MODE_READ_ONLY);
    }

    @Override public String getType(Uri uri) { return "application/vnd.android.package-archive"; }

    @Override public Cursor query(Uri uri, String[] projection, String selection, String[] args, String order) {
        MatrixCursor c = new MatrixCursor(new String[] { OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE });
        c.addRow(new Object[] { "craquez.apk", file().length() });
        return c;
    }

    @Override public Uri insert(Uri uri, ContentValues values) { return null; }
    @Override public int delete(Uri uri, String selection, String[] args) { return 0; }
    @Override public int update(Uri uri, ContentValues values, String selection, String[] args) { return 0; }
}
