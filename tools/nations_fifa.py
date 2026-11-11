"""Completa o quadro de seleções da FIFA (211) no leagues.json, sem mexer nas 40 que já existem.

    python3 tools/nations_fifa.py          (precisa de tools/league_gen.py e tools/nations.py)

Ranking aproximado (inspirado no da FIFA, não é o oficial). Jogadores fictícios com nomes típicos de cada
região; uniforme nas cores do país. Escreve também a confederação de cada seleção ("conf").
Depois: python3 tools/flags.py <flag-icons>/flags/4x3   (bandeiras em PNG)
"""
import importlib.util, json, os, random, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "tools"))
spec = importlib.util.spec_from_file_location("league_gen", os.path.join(ROOT, "tools", "league_gen.py"))
lg = importlib.util.module_from_spec(spec)
spec.loader.exec_module(lg)
import nations  # noqa: E402

DATA = os.path.join(ROOT, "Assets", "Craquez", "Resources", "Data", "leagues.json")

# grupos de nomes novos (primeiros nomes, sobrenomes)
POOLS = {
    "HU": (["Bence", "Dániel", "Ádám", "Balázs", "Zsolt", "Péter", "Gergő", "Dominik", "Roland", "Attila", "Milán", "Kristóf"],
           ["Nagy", "Kovács", "Tóth", "Szabó", "Horváth", "Varga", "Kiss", "Molnár", "Németh", "Farkas", "Balogh", "Papp"]),
    "CZ": (["Jakub", "Tomáš", "Ondřej", "Lukáš", "Patrik", "Adam", "Matěj", "Vladimír", "Marek", "Martin", "Dávid", "Michal"],
           ["Novák", "Svoboda", "Dvořák", "Černý", "Procházka", "Kučera", "Veselý", "Horák", "Hruška", "Pokorný", "Krejčí", "Šimek"]),
    "RO": (["Andrei", "Alexandru", "Ionuț", "Răzvan", "Florin", "Nicolae", "Cristian", "Valentin", "Denis", "Darius", "Vlad", "Radu"],
           ["Popescu", "Ionescu", "Stan", "Dumitru", "Stoica", "Gheorghe", "Matei", "Ciobanu", "Rusu", "Munteanu", "Moldovan", "Marin"]),
    "GR": (["Giorgos", "Dimitris", "Kostas", "Nikos", "Giannis", "Christos", "Vasilis", "Panagiotis", "Thanasis", "Stavros", "Petros", "Manolis"],
           ["Papadopoulos", "Georgiou", "Nikolaidis", "Papadakis", "Konstantinou", "Vlachos", "Oikonomou", "Makris", "Dimitriou", "Karagiannis", "Stavrou", "Antoniou"]),
    "AL": (["Arben", "Elseid", "Ardian", "Klaus", "Taulant", "Ermir", "Besart", "Valon", "Kristjan", "Endrit", "Ledion", "Arlind"],
           ["Hoxha", "Krasniqi", "Berisha", "Gashi", "Shala", "Morina", "Kelmendi", "Rama", "Bajrami", "Dushku", "Hysa", "Selmani"]),
    "GE": (["Giorgi", "Levan", "Otar", "Davit", "Luka", "Saba", "Nika", "Zurab", "Irakli", "Lasha", "Guram", "Tornike"],
           ["Beridze", "Kapanadze", "Gelashvili", "Lomidze", "Mamardashvili", "Kvaratskhava", "Tsitaishvili", "Davitashvili", "Kiteishvili", "Chakvetadze", "Lochoshvili", "Kashia"]),
    "AM": (["Aram", "Hovhannes", "Narek", "Tigran", "Artur", "Gor", "Vahan", "Karen", "Edgar", "Armen", "Hrayr", "Erik"],
           ["Hovhannisyan", "Grigoryan", "Harutyunyan", "Sargsyan", "Petrosyan", "Avetisyan", "Ghazaryan", "Mkrtchyan", "Hakobyan", "Vardanyan", "Karapetyan", "Arakelyan"]),
    "FI": (["Teemu", "Lukas", "Joel", "Robin", "Jere", "Glen", "Oliver", "Leo", "Daniel", "Joni", "Niko", "Kaan"],
           ["Korhonen", "Virtanen", "Mäkinen", "Nieminen", "Mäkelä", "Hämäläinen", "Laine", "Heikkinen", "Koskinen", "Järvinen", "Lehtonen", "Saarinen"]),
    "IS": (["Gylfi", "Aron", "Jóhann", "Hörður", "Albert", "Arnór", "Sverrir", "Hákon", "Ísak", "Rúnar", "Guðlaugur", "Birkir"],
           ["Sigurðsson", "Gunnarsson", "Jónsson", "Guðmundsson", "Þórarinsson", "Bjarnason", "Halldórsson", "Arnason", "Kristinsson", "Einarsson", "Helgason", "Ólafsson"]),
    "BALT": (["Lukas", "Mantas", "Artūrs", "Kristers", "Karl", "Rasmus", "Domantas", "Vladislavs", "Janis", "Henrik", "Edgaras", "Raivis"],
             ["Kazlauskas", "Petrauskas", "Bērziņš", "Ozols", "Tamm", "Saar", "Jankauskas", "Kalniņš", "Mägi", "Vaitkus", "Liepa", "Sepp"]),
    "IL": (["Eran", "Dor", "Manor", "Oscar", "Liel", "Yonatan", "Eli", "Shon", "Idan", "Tai", "Omri", "Gabi"],
           ["Cohen", "Levi", "Mizrahi", "Peretz", "Biton", "Dahan", "Avraham", "Friedman", "Azoulay", "Katz", "Golan", "Shaked"]),
    "CN": (["Wei", "Lei", "Jun", "Hao", "Peng", "Yang", "Bin", "Tao", "Xiang", "Chen", "Long", "Kai"],
           ["Wang", "Li", "Zhang", "Liu", "Chen", "Yang", "Zhao", "Huang", "Zhou", "Wu", "Xu", "Sun"]),
    "VN": (["Van", "Quang", "Tuan", "Hung", "Duc", "Minh", "Hoang", "Thanh", "Tien", "Cong", "Hai", "Dinh"],
           ["Nguyen", "Tran", "Le", "Pham", "Hoang", "Phan", "Vu", "Dang", "Bui", "Do", "Ho", "Ngo"]),
    "TH": (["Chanathip", "Teerasil", "Supachok", "Theerathon", "Ekanit", "Sarach", "Kritsada", "Pokklaw", "Thitipan", "Weerathep", "Suphanat", "Bordin"],
           ["Songkrasin", "Dangda", "Sarachat", "Bunmathan", "Panya", "Yooyen", "Kaman", "Anan", "Puangchan", "Pomphan", "Mueanta", "Phala"]),
    "MY": (["Ahmad", "Muhammad", "Syafiq", "Faisal", "Rizky", "Arif", "Hafiz", "Irfan", "Dimas", "Safawi", "Egy", "Witan"],
           ["Rahman", "Ismail", "Hassan", "Saputra", "Pratama", "Hidayat", "Rasid", "Kurniawan", "Wijaya", "Nugroho", "Yusof", "Ramli"]),
    "PH": (["Miguel", "Jarvey", "Mark", "Kevin", "Patrick", "Carlos", "Jefferson", "Amani", "Neil", "Daisuke", "Paul", "Oliver"],
           ["Reyes", "Santos", "Cruz", "Bautista", "Ocampo", "Garcia", "Mendoza", "Aguilar", "Villanueva", "Delos Santos", "Ramos", "Torres"]),
    "SAS": (["Sunil", "Rahul", "Anirudh", "Lallianzuala", "Sahal", "Jamal", "Anwar", "Imran", "Kiran", "Rohit", "Bikash", "Faisal"],
            ["Chhetri", "Singh", "Kumar", "Thapa", "Bheke", "Khan", "Ahmed", "Hossain", "Rai", "Gurung", "Perera", "Fernando"]),
    "MN": (["Bat-Erdene", "Tsend", "Ganbold", "Naranbold", "Temuulen", "Oyunbaatar", "Enkhjin", "Munkh", "Tuvshin", "Gantulga", "Davaa", "Bilguun"],
           ["Batbold", "Ganzorig", "Erdenebat", "Sukhbaatar", "Tserendorj", "Bayar", "Altangerel", "Dorj", "Purev", "Nyamdorj", "Baatar", "Otgon"]),
    "EAF": (["Michael", "Joseph", "Emmanuel", "Brian", "Kelvin", "Eric", "Denis", "Allan", "Samuel", "Clement", "Faustin", "Simon"],
            ["Otieno", "Mwangi", "Kamau", "Okello", "Ssempijja", "Mugisha", "Niyonzima", "Habimana", "Mbwana", "Juma", "Wanyama", "Omondi"]),
    "HORN": (["Abel", "Getaneh", "Shimelis", "Abdi", "Mohamed", "Yonas", "Daniel", "Henok", "Ahmed", "Ali", "Robel", "Bereket"],
             ["Tesfaye", "Bekele", "Kebede", "Abdullahi", "Hassan", "Gebremedhin", "Hailu", "Mengistu", "Warsame", "Farah", "Tekle", "Desta"]),
    "SAF": (["Thabo", "Sipho", "Teboho", "Percy", "Themba", "Khama", "Patson", "Kudakwashe", "Gift", "Blessing", "Lebo", "Tebogo"],
            ["Mokoena", "Dlamini", "Nkosi", "Zwane", "Mbatha", "Banda", "Phiri", "Musona", "Moyo", "Ndlovu", "Sibanda", "Mabena"]),
    "JM": (["Leon", "Michail", "Bobby", "Shamar", "Kasey", "Demarai", "Andre", "Jamal", "Kemar", "Ravel", "Javain", "Dexter"],
           ["Bailey", "Antonio", "Reid", "Nicholson", "Palmer", "Gray", "Blake", "Lowe", "Morrison", "Brown", "Campbell", "Williams"]),
    "PAC": (["Roy", "Iosefa", "Sione", "Malakai", "Tevita", "Raphael", "Atkin", "Kiko", "Joseph", "Rewa", "Taniela", "Micah"],
            ["Krishna", "Tuilagi", "Fifita", "Vaka", "Naidu", "Tavita", "Kaltak", "Seko", "Wale", "Ravula", "Lealaiauloto", "Moala"]),
}

# país -> (nome em português, sigla, iso, cor1, cor2, grupo de nomes, rosto de referência, confederação)
# (iso de Escócia/País de Gales/Irlanda do Norte: XS/XW/XN, desenhados das bandeiras gb-sct/gb-wls/gb-nir)
NEW = [
    ("Irã", "IRA", "IR", "#ffffff", "#239f40", "IR", "IR", "AFC"),
    ("País de Gales", "GAL", "XW", "#c8102e", "#00ad36", "EN", "EN", "UEFA"),
    ("Sérvia", "SER", "RS", "#c6363c", "#0c4076", "HR", "HR", "UEFA"),
    ("Panamá", "PAN", "PA", "#da121a", "#ffffff", "MX", "CO", "CONCACAF"),
    ("Hungria", "HUN", "HU", "#ce2939", "#477050", "HU", "DE", "UEFA"),
    ("Argélia", "ARL", "DZ", "#ffffff", "#006233", "MA", "MA", "CAF"),
    ("Tchéquia", "TCH", "CZ", "#d7141a", "#11457e", "CZ", "DE", "UEFA"),
    ("Escócia", "ESC", "XS", "#0065bf", "#ffffff", "EN", "EN", "UEFA"),
    ("Tunísia", "TUN", "TN", "#e70013", "#ffffff", "MA", "MA", "CAF"),
    ("Grécia", "GRE", "GR", "#0d5eaf", "#ffffff", "GR", "IT", "UEFA"),
    ("Eslováquia", "EVQ", "SK", "#0b4ea2", "#ffffff", "CZ", "DE", "UEFA"),
    ("Romênia", "ROM", "RO", "#fcd116", "#002b7f", "RO", "IT", "UEFA"),
    ("Venezuela", "VEN", "VE", "#7b1e2b", "#ffcc00", "CO", "CO", "CONMEBOL"),
    ("Catar", "CAT", "QA", "#8a1538", "#ffffff", "SA", "SA", "AFC"),
    ("Costa Rica", "CRC", "CR", "#ce1126", "#002b7f", "MX", "CO", "CONCACAF"),
    ("Mali", "MLI", "ML", "#14b53a", "#fcd116", "SN", "SN", "CAF"),
    ("Eslovênia", "EVN", "SI", "#ffffff", "#005ce5", "HR", "HR", "UEFA"),
    ("Iraque", "IRQ", "IQ", "#007a3d", "#ffffff", "SA", "SA", "AFC"),
    ("África do Sul", "AFS", "ZA", "#ffb81c", "#007749", "SAF", "NG", "CAF"),
    ("Uzbequistão", "UZB", "UZ", "#ffffff", "#0099b5", "KZ", "KZ", "AFC"),
    ("RD Congo", "RDC", "CD", "#007fff", "#ce1021", "CM", "CM", "CAF"),
    ("Irlanda", "IRL", "IE", "#169b62", "#ffffff", "EN", "EN", "UEFA"),
    ("Burkina Faso", "BFA", "BF", "#ef2b2d", "#009e49", "SN", "SN", "CAF"),
    ("Albânia", "ALB", "AL", "#e41e20", "#111111", "AL", "HR", "UEFA"),
    ("Jordânia", "JOR", "JO", "#ffffff", "#ce1126", "SA", "SA", "AFC"),
    ("Emirados Árabes", "EAU", "AE", "#ffffff", "#00732f", "SA", "SA", "AFC"),
    ("Macedônia do Norte", "MCN", "MK", "#d20000", "#ffe600", "HR", "HR", "UEFA"),
    ("Geórgia", "GEO", "GE", "#ffffff", "#e8112d", "GE", "HR", "UEFA"),
    ("Jamaica", "JAM", "JM", "#fed100", "#009b3a", "JM", "NG", "CONCACAF"),
    ("Finlândia", "FIN", "FI", "#ffffff", "#003580", "FI", "SE", "UEFA"),
    ("Bósnia", "BOS", "BA", "#002395", "#fecb00", "HR", "HR", "UEFA"),
    ("Islândia", "ISL", "IS", "#02529c", "#dc1e35", "IS", "NO", "UEFA"),
    ("Honduras", "HON", "HN", "#ffffff", "#0073cf", "MX", "CO", "CONCACAF"),
    ("Cabo Verde", "CVE", "CV", "#003893", "#cf2027", "PT", "SN", "CAF"),
    ("Israel", "ISR", "IL", "#ffffff", "#0038b8", "IL", "IT", "UEFA"),
    ("Irlanda do Norte", "IRN", "XN", "#00843d", "#ffffff", "EN", "EN", "UEFA"),
    ("Montenegro", "MNE", "ME", "#c40308", "#d4af37", "HR", "HR", "UEFA"),
    ("Bolívia", "BOL", "BO", "#007934", "#ffffff", "PE", "PE", "CONMEBOL"),
    ("Omã", "OMA", "OM", "#db161b", "#ffffff", "SA", "SA", "AFC"),
    ("Gabão", "GAB", "GA", "#ffd700", "#009e60", "CM", "CM", "CAF"),
    ("Guiné", "GUI", "GN", "#ce1126", "#fcd116", "SN", "SN", "CAF"),
    ("Angola", "ANG", "AO", "#cc092f", "#111111", "PT", "NG", "CAF"),
    ("Zâmbia", "ZAM", "ZM", "#198a00", "#ef7d00", "SAF", "NG", "CAF"),
    ("Uganda", "UGA", "UG", "#fcdc04", "#111111", "EAF", "NG", "CAF"),
    ("Bahrein", "BAR", "BH", "#ce1126", "#ffffff", "SA", "SA", "AFC"),
    ("Benin", "BEN", "BJ", "#fcd116", "#008751", "SN", "SN", "CAF"),
    ("Bulgária", "BUL", "BG", "#ffffff", "#00966e", "RU", "HR", "UEFA"),
    ("Guiné Equatorial", "GEQ", "GQ", "#e32118", "#3e9a00", "ES", "CM", "CAF"),
    ("Síria", "SIR", "SY", "#ce1126", "#ffffff", "SA", "SA", "AFC"),
    ("El Salvador", "SLV", "SV", "#0f47af", "#ffffff", "MX", "CO", "CONCACAF"),
    ("Haiti", "HAI", "HT", "#00209f", "#d21034", "FR", "NG", "CONCACAF"),
    ("Luxemburgo", "LUX", "LU", "#ef3340", "#00a3e0", "FR", "FR", "UEFA"),
    ("China", "CHN", "CN", "#de2910", "#ffde00", "CN", "JP", "AFC"),
    ("Palestina", "PLE", "PS", "#ffffff", "#007a3d", "SA", "SA", "AFC"),
    ("Quirguistão", "QUI", "KG", "#e8112d", "#ffef00", "KZ", "KZ", "AFC"),
    ("Belarus", "BLR", "BY", "#c8313e", "#4aa657", "RU", "RU", "UEFA"),
    ("Kosovo", "KOS", "XK", "#244aa5", "#d0a650", "AL", "HR", "UEFA"),
    ("Curaçao", "CUW", "CW", "#002b7f", "#f9e814", "NL", "NG", "CONCACAF"),
    ("Tailândia", "TAI", "TH", "#241d4f", "#ffffff", "TH", "MX", "AFC"),
    ("Moçambique", "MOZ", "MZ", "#d21034", "#111111", "PT", "NG", "CAF"),
    ("Madagascar", "MAD", "MG", "#fc3d32", "#007e3a", "FR", "SN", "CAF"),
    ("Quênia", "QUE", "KE", "#bb0000", "#006600", "EAF", "NG", "CAF"),
    ("Vietnã", "VIE", "VN", "#da251d", "#ffff00", "VN", "JP", "AFC"),
    ("Tadjiquistão", "TJK", "TJ", "#cc0000", "#ffffff", "KZ", "KZ", "AFC"),
    ("Armênia", "ARM", "AM", "#d90012", "#0033a0", "AM", "HR", "UEFA"),
    ("Mauritânia", "MTN", "MR", "#006233", "#ffc400", "MA", "MA", "CAF"),
    ("Líbano", "LIB", "LB", "#ee161f", "#ffffff", "SA", "SA", "AFC"),
    ("Nova Zelândia", "NZL", "NZ", "#ffffff", "#111111", "AU", "AU", "OFC"),
    ("Guiné-Bissau", "GBS", "GW", "#ce1126", "#fcd116", "PT", "SN", "CAF"),
    ("Coreia do Norte", "PRK", "KP", "#ed1c27", "#ffffff", "KR", "KR", "AFC"),
    ("Trinidad e Tobago", "TRI", "TT", "#da1a35", "#111111", "JM", "NG", "CONCACAF"),
    ("Cazaquistão", "CAZ", "KZ", "#00afca", "#fec50c", "KZ", "KZ", "UEFA"),
    ("Togo", "TOG", "TG", "#006a4e", "#ffce00", "SN", "SN", "CAF"),
    ("Guatemala", "GUA", "GT", "#4997d0", "#ffffff", "MX", "CO", "CONCACAF"),
    ("Serra Leoa", "SLE", "SL", "#1eb53a", "#0072c6", "GH", "NG", "CAF"),
    ("Tanzânia", "TAN", "TZ", "#1eb53a", "#00a3dd", "EAF", "NG", "CAF"),
    ("Namíbia", "NAM", "NA", "#003580", "#d21034", "SAF", "NG", "CAF"),
    ("Líbia", "LBA", "LY", "#e70013", "#111111", "MA", "MA", "CAF"),
    ("Gâmbia", "GAM", "GM", "#ce1126", "#0c1c8c", "SN", "SN", "CAF"),
    ("Sudão", "SUD", "SD", "#d21034", "#ffffff", "EG", "SN", "CAF"),
    ("Zimbábue", "ZIM", "ZW", "#ffd200", "#319208", "SAF", "NG", "CAF"),
    ("Estônia", "EST", "EE", "#0072ce", "#111111", "BALT", "SE", "UEFA"),
    ("Azerbaijão", "AZE", "AZ", "#00b5e2", "#ef3340", "TR", "TR", "UEFA"),
    ("Ilhas Faroé", "FAR", "FO", "#ffffff", "#0065bd", "DK", "NO", "UEFA"),
    ("Níger", "NIE", "NE", "#e05206", "#0db02b", "SN", "SN", "CAF"),
    ("Ruanda", "RUA", "RW", "#00a1de", "#fad201", "EAF", "NG", "CAF"),
    ("Comores", "COM", "KM", "#3a75c4", "#ffc61e", "FR", "SN", "CAF"),
    ("Kuwait", "KUW", "KW", "#007a3d", "#ffffff", "SA", "SA", "AFC"),
    ("Chipre", "CHP", "CY", "#ffffff", "#4e5b31", "GR", "IT", "UEFA"),
    ("Malawi", "MAW", "MW", "#ce1126", "#111111", "SAF", "NG", "CAF"),
    ("Índia", "IND", "IN", "#0047ab", "#ff9933", "SAS", "IR", "AFC"),
    ("Malásia", "MAS", "MY", "#ffd700", "#010066", "MY", "MX", "AFC"),
    ("Suriname", "SUR", "SR", "#377e3f", "#b40a2d", "NL", "NG", "CONCACAF"),
    ("Turcomenistão", "TKM", "TM", "#00843d", "#ffffff", "KZ", "KZ", "AFC"),
    ("Lituânia", "LTU", "LT", "#fdb913", "#006a44", "BALT", "SE", "UEFA"),
    ("Letônia", "LET", "LV", "#9e3039", "#ffffff", "BALT", "SE", "UEFA"),
    ("Nicarágua", "NCA", "NI", "#0067c6", "#ffffff", "MX", "CO", "CONCACAF"),
    ("Indonésia", "IDN", "ID", "#ce1126", "#ffffff", "MY", "MX", "AFC"),
    ("Filipinas", "FIL", "PH", "#0038a8", "#ce1126", "PH", "MX", "AFC"),
    ("Etiópia", "ETI", "ET", "#078930", "#fcdd09", "HORN", "SN", "CAF"),
    ("Burundi", "BDI", "BI", "#ce1126", "#1eb53a", "EAF", "NG", "CAF"),
    ("Congo", "CGO", "CG", "#009543", "#fbde4a", "CM", "CM", "CAF"),
    ("Essuatíni", "SWZ", "SZ", "#3e5eb9", "#ffd900", "SAF", "NG", "CAF"),
    ("Lesoto", "LES", "LS", "#00209f", "#009543", "SAF", "NG", "CAF"),
    ("Libéria", "LBR", "LR", "#bf0a30", "#002868", "GH", "NG", "CAF"),
    ("República Centro-Africana", "RCA", "CF", "#003082", "#ffce00", "CM", "CM", "CAF"),
    ("Botsuana", "BOT", "BW", "#75aadb", "#111111", "SAF", "NG", "CAF"),
    ("Sudão do Sul", "SSD", "SS", "#0f47af", "#078930", "EAF", "NG", "CAF"),
    ("Moldávia", "MDA", "MD", "#0046ae", "#ffd200", "RO", "RU", "UEFA"),
    ("República Dominicana", "DOM", "DO", "#002d62", "#ce1126", "MX", "BR", "CONCACAF"),
    ("Hong Kong", "HKG", "HK", "#de2910", "#ffffff", "CN", "JP", "AFC"),
    ("Afeganistão", "AFG", "AF", "#111111", "#d32011", "SAS", "IR", "AFC"),
    ("Iêmen", "IEM", "YE", "#ce1126", "#ffffff", "SA", "SA", "AFC"),
    ("Taipé Chinesa", "TPE", "TW", "#0055a4", "#fe0000", "CN", "JP", "AFC"),
    ("Maldivas", "MDV", "MV", "#d21034", "#007e3a", "SAS", "IR", "AFC"),
    ("Porto Rico", "PUR", "PR", "#ed0000", "#0050f0", "MX", "BR", "CONCACAF"),
    ("Mianmar", "MYA", "MM", "#fecb00", "#ea2839", "TH", "MX", "AFC"),
    ("Singapura", "SIN", "SG", "#ef3340", "#ffffff", "MY", "JP", "AFC"),
    ("Ilhas Salomão", "SOL", "SB", "#0051ba", "#fcd116", "PAC", "BR", "OFC"),
    ("Taiti", "TAH", "PF", "#ce1126", "#ffffff", "FR", "BR", "OFC"),
    ("Papua-Nova Guiné", "PNG", "PG", "#ce1126", "#111111", "PAC", "BR", "OFC"),
    ("Fiji", "FIJ", "FJ", "#ffffff", "#62b5e5", "PAC", "BR", "OFC"),
    ("Vanuatu", "VAN", "VU", "#d21034", "#009543", "PAC", "BR", "OFC"),
    ("Nova Caledônia", "NCL", "NC", "#0035ad", "#ed4135", "FR", "BR", "OFC"),
    ("Andorra", "AND", "AD", "#10069f", "#fedf00", "ES", "ES", "UEFA"),
    ("Malta", "MLT", "MT", "#ffffff", "#cf142b", "IT", "IT", "UEFA"),
    ("Bangladesh", "BAN", "BD", "#006a4e", "#f42a41", "SAS", "IR", "AFC"),
    ("Nepal", "NEP", "NP", "#dc143c", "#003893", "SAS", "IR", "AFC"),
    ("Sri Lanka", "SRI", "LK", "#8d153a", "#ffb700", "SAS", "IR", "AFC"),
    ("Butão", "BUT", "BT", "#ff4e12", "#ffd520", "SAS", "IR", "AFC"),
    ("Paquistão", "PAQ", "PK", "#01411c", "#ffffff", "SAS", "IR", "AFC"),
    ("Laos", "LAO", "LA", "#ce1126", "#002868", "TH", "MX", "AFC"),
    ("Camboja", "CAM", "KH", "#032ea1", "#e00025", "TH", "MX", "AFC"),
    ("Brunei", "BRU", "BN", "#f7e017", "#111111", "MY", "MX", "AFC"),
    ("Mongólia", "MGL", "MN", "#c4272f", "#015197", "MN", "KZ", "AFC"),
    ("Timor-Leste", "TLS", "TL", "#dc241f", "#111111", "PT", "MX", "AFC"),
    ("Macau", "MAC", "MO", "#00785e", "#ffffff", "CN", "JP", "AFC"),
    ("Guam", "GUM", "GU", "#00297b", "#c62139", "US", "MX", "AFC"),
    ("Samoa", "SAM", "WS", "#ce1126", "#002b7f", "PAC", "BR", "OFC"),
    ("Tonga", "TGA", "TO", "#c10000", "#ffffff", "PAC", "BR", "OFC"),
    ("Samoa Americana", "ASA", "AS", "#002b7f", "#bf0a30", "PAC", "BR", "OFC"),
    ("Ilhas Cook", "COK", "CK", "#012169", "#ffffff", "PAC", "BR", "OFC"),
    ("Liechtenstein", "LIE", "LI", "#002b7f", "#ce1126", "DE", "DE", "UEFA"),
    ("San Marino", "SMR", "SM", "#5eb6e4", "#ffffff", "IT", "IT", "UEFA"),
    ("Gibraltar", "GIB", "GI", "#da000c", "#ffffff", "EN", "ES", "UEFA"),
    ("Maurício", "MRI", "MU", "#ea2839", "#1a206d", "FR", "SN", "CAF"),
    ("Seicheles", "SEY", "SC", "#003f87", "#fcd856", "FR", "SN", "CAF"),
    ("Djibuti", "DJI", "DJ", "#6ab2e7", "#12ad2b", "HORN", "SN", "CAF"),
    ("Somália", "SOM", "SO", "#4189dd", "#ffffff", "HORN", "SN", "CAF"),
    ("Eritreia", "ERI", "ER", "#12ad2b", "#4189dd", "HORN", "SN", "CAF"),
    ("São Tomé e Príncipe", "STP", "ST", "#12ad2b", "#ffce00", "PT", "NG", "CAF"),
    ("Chade", "CHA", "TD", "#002664", "#fecb00", "SN", "SN", "CAF"),
    ("Cuba", "CUB", "CU", "#cf142b", "#002a8f", "MX", "BR", "CONCACAF"),
    ("Granada", "GRN", "GD", "#ce1126", "#fcd116", "JM", "NG", "CONCACAF"),
    ("São Cristóvão e Névis", "SKN", "KN", "#009e49", "#ce1126", "JM", "NG", "CONCACAF"),
    ("Santa Lúcia", "LCA", "LC", "#66ccff", "#fcd116", "JM", "NG", "CONCACAF"),
    ("São Vicente e Granadinas", "VIN", "VC", "#fcd116", "#009e60", "JM", "NG", "CONCACAF"),
    ("Antígua e Barbuda", "ATG", "AG", "#ce1126", "#111111", "JM", "NG", "CONCACAF"),
    ("Barbados", "BRB", "BB", "#00267f", "#ffc726", "JM", "NG", "CONCACAF"),
    ("Bahamas", "BAH", "BS", "#00abc9", "#fae042", "JM", "NG", "CONCACAF"),
    ("Belize", "BLZ", "BZ", "#003f87", "#ce1126", "JM", "BR", "CONCACAF"),
    ("Guiana", "GUY", "GY", "#009e49", "#fcd116", "JM", "NG", "CONCACAF"),
    ("Dominica", "DMA", "DM", "#006b3f", "#fcd116", "JM", "NG", "CONCACAF"),
    ("Aruba", "ARU", "AW", "#418fde", "#fbe122", "NL", "BR", "CONCACAF"),
    ("Bermudas", "BER", "BM", "#c8102e", "#ffffff", "JM", "NG", "CONCACAF"),
    ("Ilhas Cayman", "CAY", "KY", "#c8102e", "#012169", "JM", "NG", "CONCACAF"),
    ("Montserrat", "MSR", "MS", "#012169", "#00a650", "JM", "NG", "CONCACAF"),
    ("Turcas e Caicos", "TCA", "TC", "#012169", "#ffffff", "JM", "NG", "CONCACAF"),
    ("Ilhas Virgens Britânicas", "VGB", "VG", "#012169", "#00ab4e", "JM", "NG", "CONCACAF"),
    ("Ilhas Virgens Americanas", "VIR", "VI", "#ffffff", "#162e51", "JM", "NG", "CONCACAF"),
    ("Anguila", "AIA", "AI", "#012169", "#ff9e1b", "JM", "NG", "CONCACAF"),
]

CONF_PT = {"UEFA": "Europa", "CONMEBOL": "América do Sul", "CONCACAF": "América do Norte e Central",
           "CAF": "África", "AFC": "Ásia e Oceania", "OFC": "Ásia e Oceania"}


def main():
    for k, (f, l) in POOLS.items():
        lg.NAMES[k] = (f, l, [])
    lg.NAMES.update({k: v for k, v in nations.EXTRA_NAMES.items()})
    d = json.load(open(DATA, encoding="utf-8"))
    sel = next(l for l in d["leagues"] if l["id"] == "sel")
    have = {t["name"] for t in sel["teams"]}
    lib = lg.face_gen.write_library(360, 2026, __import__("pathlib").Path("/tmp") / "craquez_faces")
    positions = lg.POSITIONS + ["GK", "DF", "MF", "MF", "FW"]
    base = len(sel["teams"])
    rows = [r for r in NEW if r[0] not in have]
    n = len(rows)
    added = 0
    for i, (name, short, iso, c1, c2, pool, face, conf) in enumerate(rows):
        rank = base + 1 + i
        rating = round(0.705 - i / max(1, n - 1) * 0.255, 3)   # 0,705 .. 0,45
        r = random.Random(f"fifa-{name}")
        used: set = set()
        nums = {k: list(v) for k, v in lg.NUMBERS.items()}
        roster = []
        for pi, pos in enumerate(positions):
            f = lg.face_for(r, face)
            num = nums[pos].pop(0) if nums[pos] else 27 + pi
            star = 0.06 if pi in (8, 9, 14, 15, 5, 10) else 0.0
            a = lg.attrs(r, rating + star, pos)
            roster.append({"n": lg.player_name(r, pool, used), "num": num, "pos": pos, "nat": iso,
                           "f": f, "fid": lg.nearest_face(lib, f), "a": a, "ovr": lg.overall(pos, a),
                           "t": lg.traits_for(r, pos, a, star)})
        kits = lg.kits_for(r, c1, c2)
        cap = int(r.uniform(12, 70) if rating > 0.6 else r.uniform(5, 30)) * 1000
        sel["teams"].append({"name": name, "short": short, "c1": c1, "c2": c2, "rating": rating, "kits": kits,
                             "rank": rank, "flag": ["h", c1, c2], "conf": CONF_PT[conf], "iso": iso,
                             "stadium": {"name": f"Estádio Nacional ({name})", "tier": 3 if rating > 0.62 else 2, "cap": cap},
                             "roster": roster})
        added += 1
    json.dump(d, open(DATA, "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))
    print(f"{added} seleções novas; total {len(sel['teams'])}")


if __name__ == "__main__":
    main()
