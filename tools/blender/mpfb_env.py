"""Acesso ao MPFB (MakeHuman para Blender) a partir do bpy em modo headless.

O MPFB e os assets do MakeHuman são CC0; ver tools/blender/README.md para a
instalação (bpy 5.2 + extensão mpfb + pacote makehuman_system_assets).
"""
import importlib
import sys

import addon_utils

addon_utils.enable("bl_ext.user_default.mpfb", default_set=True)


def mpfb(path: str, key: str):
    for name in list(sys.modules):
        if name.endswith(path):
            return getattr(importlib.import_module(name), key)
    raise ImportError(path)


HumanService = mpfb("mpfb.services.humanservice", "HumanService")
TargetService = mpfb("mpfb.services.targetservice", "TargetService")
AssetService = mpfb("mpfb.services.assetservice", "AssetService")
ExportService = mpfb("mpfb.services.exportservice", "ExportService")
ObjectService = mpfb("mpfb.services.objectservice", "ObjectService")
HumanObjectProperties = mpfb("mpfb.entities.objectproperties", "HumanObjectProperties")
