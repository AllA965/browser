#!/usr/bin/env python3
import json
from pathlib import Path

BASE = Path(__file__).resolve().parents[1] / "Resources" / "i18n"

KEYS = [
    "settings.header.title",
    "settings.header.search_placeholder",
    "settings.title",
    "settings.search_placeholder",
    "settings.sidebar.history",
    "settings.sidebar.settings",
    "settings.sidebar.ai",
    "settings.sidebar.privacy",
    "settings.sidebar.advanced",
    "settings.appearance.language",
    "settings.appearance.language_auto",
    "settings.appearance.language_zhCN",
    "settings.appearance.language_en",
    "appearance.language",
    "appearance.language_auto",
    "appearance.language_zhCN",
    "appearance.language_en",
]

T = {
    "en": ["Settings", "Search settings", "Settings", "Search settings", "History", "Settings", "AI Settings", "Privacy", "Advanced", "Language", "Follow system", "Chinese (Simplified)", "English", "Language", "Follow system", "Chinese (Simplified)", "English"],
    "zh-CN": ["设置", "搜索设置", "设置", "搜索设置", "历史记录", "设置", "AI 设置", "隐私", "高级", "语言", "跟随系统", "中文（简体）", "English", "语言", "跟随系统", "中文（简体）", "English"],
    "zh_CN": ["设置", "搜索设置", "设置", "搜索设置", "历史记录", "设置", "AI 设置", "隐私", "高级", "语言", "跟随系统", "中文（简体）", "English", "语言", "跟随系统", "中文（简体）", "English"],
    "zh_TW": ["設定", "搜尋設定", "設定", "搜尋設定", "歷史記錄", "設定", "AI 設定", "隱私", "進階", "語言", "跟隨系統", "中文（簡體）", "English", "語言", "跟隨系統", "中文（簡體）", "English"],
    "ja": ["設定", "設定を検索", "設定", "設定を検索", "履歴", "設定", "AI 設定", "プライバシー", "詳細設定", "言語", "システムに従う", "中国語（簡体字）", "英語", "言語", "システムに従う", "中国語（簡体字）", "英語"],
    "ko": ["설정", "설정 검색", "설정", "설정 검색", "기록", "설정", "AI 설정", "개인정보", "고급", "언어", "시스템 따르기", "중국어(간체)", "영어", "언어", "시스템 따르기", "중국어(간체)", "영어"],
    "fr": ["Paramètres", "Rechercher dans les paramètres", "Paramètres", "Rechercher dans les paramètres", "Historique", "Paramètres", "Paramètres IA", "Confidentialité", "Avancé", "Langue", "Suivre le système", "Chinois (simplifié)", "Anglais", "Langue", "Suivre le système", "Chinois (simplifié)", "Anglais"],
    "de": ["Einstellungen", "Einstellungen durchsuchen", "Einstellungen", "Einstellungen durchsuchen", "Verlauf", "Einstellungen", "KI-Einstellungen", "Datenschutz", "Erweitert", "Sprache", "System folgen", "Chinesisch (Vereinfacht)", "Englisch", "Sprache", "System folgen", "Chinesisch (Vereinfacht)", "Englisch"],
    "es": ["Configuración", "Buscar en la configuración", "Configuración", "Buscar en la configuración", "Historial", "Configuración", "Configuración de IA", "Privacidad", "Avanzado", "Idioma", "Seguir sistema", "Chino (simplificado)", "Inglés", "Idioma", "Seguir sistema", "Chino (simplificado)", "Inglés"],
    "ru": ["Настройки", "Поиск настроек", "Настройки", "Поиск настроек", "История", "Настройки", "Настройки ИИ", "Конфиденциальность", "Дополнительно", "Язык", "Следовать системе", "Китайский (упрощённый)", "Английский", "Язык", "Следовать системе", "Китайский (упрощённый)", "Английский"],
    "ar": ["الإعدادات", "بحث في الإعدادات", "الإعدادات", "بحث في الإعدادات", "السجل", "الإعدادات", "إعدادات الذكاء الاصطناعي", "الخصوصية", "متقدم", "اللغة", "اتباع النظام", "الصينية (المبسطة)", "الإنجليزية", "اللغة", "اتباع النظام", "الصينية (المبسطة)", "الإنجليزية"],
    "bn": ["সেটিংস", "সেটিংস অনুসন্ধান করুন", "সেটিংস", "সেটিংস অনুসন্ধান করুন", "ইতিহাস", "সেটিংস", "AI সেটিংস", "গোপনীয়তা", "উন্নত", "ভাষা", "সিস্টেম অনুসরণ করুন", "চীনা (সরলীকৃত)", "ইংরেজি", "ভাষা", "সিস্টেম অনুসরণ করুন", "চীনা (সরলীকৃত)", "ইংরেজি"],
    "pt": ["Configurações", "Pesquisar configurações", "Configurações", "Pesquisar configurações", "Histórico", "Configurações", "Configurações de IA", "Privacidade", "Avançado", "Idioma", "Seguir sistema", "Chinês (simplificado)", "Inglês", "Idioma", "Seguir sistema", "Chinês (simplificado)", "Inglês"],
    "pt_BR": ["Configurações", "Pesquisar configurações", "Configurações", "Pesquisar configurações", "Histórico", "Configurações", "Configurações de IA", "Privacidade", "Avançado", "Idioma", "Seguir sistema", "Chinês (simplificado)", "Inglês", "Idioma", "Seguir sistema", "Chinês (simplificado)", "Inglês"],
    "id": ["Pengaturan", "Cari pengaturan", "Pengaturan", "Cari pengaturan", "Riwayat", "Pengaturan", "Pengaturan AI", "Privasi", "Lanjutan", "Bahasa", "Ikuti sistem", "Tionghoa (Sederhana)", "Inggris", "Bahasa", "Ikuti sistem", "Tionghoa (Sederhana)", "Inggris"],
    "ur": ["ترتیبات", "ترتیبات تلاش کریں", "ترتیبات", "ترتیبات تلاش کریں", "تاریخچہ", "ترتیبات", "AI ترتیبات", "رازداری", "اعلیٰ", "زبان", "سسٹم کے مطابق", "چینی (سادہ)", "انگریزی", "زبان", "سسٹم کے مطابق", "چینی (سادہ)", "انگریزی"],
    "hi": ["सेटिंग्स", "सेटिंग्स खोजें", "सेटिंग्स", "सेटिंग्स खोजें", "इतिहास", "सेटिंग्स", "AI सेटिंग्स", "गोपनीयता", "उन्नत", "भाषा", "सिस्टम का अनुसरण करें", "चीनी (सरलीकृत)", "अंग्रेज़ी", "भाषा", "सिस्टम का अनुसरण करें", "चीनी (सरलीकृत)", "अंग्रेज़ी"],
    "tr": ["Ayarlar", "Ayarlarda ara", "Ayarlar", "Ayarlarda ara", "Geçmiş", "Ayarlar", "Yapay Zeka Ayarları", "Gizlilik", "Gelişmiş", "Dil", "Sistemi takip et", "Çince (Basitleştirilmiş)", "İngilizce", "Dil", "Sistemi takip et", "Çince (Basitleştirilmiş)", "İngilizce"],
    "vi": ["Cài đặt", "Tìm kiếm cài đặt", "Cài đặt", "Tìm kiếm cài đặt", "Lịch sử", "Cài đặt", "Cài đặt AI", "Quyền riêng tư", "Nâng cao", "Ngôn ngữ", "Theo hệ thống", "Tiếng Trung (Giản thể)", "Tiếng Anh", "Ngôn ngữ", "Theo hệ thống", "Tiếng Trung (Giản thể)", "Tiếng Anh"],
    "th": ["การตั้งค่า", "ค้นหาการตั้งค่า", "การตั้งค่า", "ค้นหาการตั้งค่า", "ประวัติ", "การตั้งค่า", "การตั้งค่า AI", "ความเป็นส่วนตัว", "ขั้นสูง", "ภาษา", "ตามระบบ", "จีน (ตัวย่อ)", "อังกฤษ", "ภาษา", "ตามระบบ", "จีน (ตัวย่อ)", "อังกฤษ"],
    "it": ["Impostazioni", "Cerca impostazioni", "Impostazioni", "Cerca impostazioni", "Cronologia", "Impostazioni", "Impostazioni IA", "Privacy", "Avanzate", "Lingua", "Segui sistema", "Cinese (semplificato)", "Inglese", "Lingua", "Segui sistema", "Cinese (semplificato)", "Inglese"],
    "fa": ["تنظیمات", "جستجو در تنظیمات", "تنظیمات", "جستجو در تنظیمات", "تاریخچه", "تنظیمات", "تنظیمات هوش مصنوعی", "حریم خصوصی", "پیشرفته", "زبان", "پیروی از سیستم", "چینی (ساده‌شده)", "انگلیسی", "زبان", "پیروی از سیستم", "چینی (ساده‌شده)", "انگلیسی"],
    "sw": ["Mipangilio", "Tafuta mipangilio", "Mipangilio", "Tafuta mipangilio", "Historia", "Mipangilio", "Mipangilio ya AI", "Faragha", "Ya hali ya juu", "Lugha", "Fuata mfumo", "Kichina (Kilichorahisishwa)", "Kiingereza", "Lugha", "Fuata mfumo", "Kichina (Kilichorahisishwa)", "Kiingereza"],
    "tl": ["Mga Setting", "Maghanap ng settings", "Mga Setting", "Maghanap ng settings", "Kasaysayan", "Mga Setting", "AI Settings", "Privacy", "Advanced", "Wika", "Sundin ang system", "Chinese (Pinasimple)", "English", "Wika", "Sundin ang system", "Chinese (Pinasimple)", "English"],
    "ta": ["அமைப்புகள்", "அமைப்புகளை தேடுக", "அமைப்புகள்", "அமைப்புகளை தேடுக", "வரலாறு", "அமைப்புகள்", "AI அமைப்புகள்", "தனியுரிமை", "மேம்பட்ட", "மொழி", "கணினி அமைப்பைப் பின்பற்று", "சீனம் (எளிமைப்படுத்தப்பட்டது)", "ஆங்கிலம்", "மொழி", "கணினி அமைப்பைப் பின்பற்று", "சீனம் (எளிமைப்படுத்தப்பட்டது)", "ஆங்கிலம்"],
    "jv": ["Setelan", "Goleki setelan", "Setelan", "Goleki setelan", "Riwayat", "Setelan", "Setelan AI", "Privasi", "Lanjut", "Basa", "Tindakake sistem", "Cina (Disederhanakake)", "Inggris", "Basa", "Tindakake sistem", "Cina (Disederhanakake)", "Inggris"],
    "ms": ["Tetapan", "Cari tetapan", "Tetapan", "Cari tetapan", "Sejarah", "Tetapan", "Tetapan AI", "Privasi", "Lanjutan", "Bahasa", "Ikut sistem", "Cina (Ringkas)", "Inggeris", "Bahasa", "Ikut sistem", "Cina (Ringkas)", "Inggeris"],
    "ha": ["Saituna", "Bincika saituna", "Saituna", "Bincika saituna", "Tarihi", "Saituna", "Saitunan AI", "Sirri", "Na gaba", "Harshe", "Bi tsarin kwamfuta", "Sinanci (Sauƙaƙe)", "Turanci", "Harshe", "Bi tsarin kwamfuta", "Sinanci (Sauƙaƙe)", "Turanci"],
}


def set_key(obj, key, value):
    parts = key.split(".")
    cur = obj
    for p in parts[:-1]:
        if p not in cur or not isinstance(cur[p], dict):
            cur[p] = {}
        cur = cur[p]
    cur[parts[-1]] = value


updated = 0
for path in BASE.glob("*.json"):
    if path.name.endswith(".issues.json"):
        continue
    code = path.stem
    if code not in T:
        continue
    data = json.loads(path.read_text(encoding="utf-8"))
    vals = T[code]
    for i, k in enumerate(KEYS):
        set_key(data, k, vals[i])
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    updated += 1

print(f"updated={updated}")

# Additional manual fixes for high-frequency problematic keys.
EXTRA = {
    "ar": {
        "bookmarks.added_message": "تمت إضافة {{count}} علامة تبويب إلى المجلد \"{{folder}}\"",
        "video_dialog.parse_failed": "فشل تحليل معلومات الفيديو: {{msg}}",
    },
    "ja": {
        "bookmarks.added_message": "{{count}} 個のタブをフォルダー「{{folder}}」に追加しました",
        "video_dialog.parse_failed": "動画情報の解析に失敗しました: {{msg}}",
        "site_settings.desc": "特定のサイトに対して {{category}} の権限を設定できます。",
        "address_dropdown.search": "「{{text}}」を検索",
    },
}

extra_updated = 0
for code, mapping in EXTRA.items():
    path = BASE / f"{code}.json"
    if not path.exists():
        continue
    data = json.loads(path.read_text(encoding="utf-8"))
    for key, value in mapping.items():
        set_key(data, key, value)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    extra_updated += 1

print(f"extra_updated={extra_updated}")
