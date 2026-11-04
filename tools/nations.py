"""Seleções nacionais (futebol e futsal) para o Craquez.

Ranking próprio do jogo, inspirado no ranking FIFA (não é o oficial). Jogadores
fictícios gerados com nomes típicos de cada país. Bandeiras estilizadas:
  flag = [layout, cores...]  layouts: h (faixas horizontais), v (verticais),
  nordic (cruz escandinava), cross (cruz central), disc (fundo + círculo),
  diag (diagonal), star (fundo + estrela), hstar (faixas + estrela).
"""

# pools extras de nomes (primeiros nomes, sobrenomes, apelidos)
EXTRA_NAMES = {
    "BE": (["Lucas", "Thomas", "Arthur", "Louis", "Maxime", "Jens", "Wout", "Kevin", "Dries", "Arne", "Senne", "Robin", "Mats", "Siebe", "Noah"],
           ["Peeters", "Janssens", "Maes", "Jacobs", "Mertens", "Willems", "Claes", "Goossens", "Wouters", "De Smet", "Dubois", "Lambert", "Dupont", "Vermeulen", "Hermans"], []),
    "HR": (["Luka", "Ivan", "Marko", "Josip", "Ante", "Mateo", "Nikola", "Filip", "Dario", "Petar", "Toni", "Domagoj", "Karlo", "Lovro", "Bruno"],
           ["Horvat", "Kovačević", "Babić", "Marić", "Jurić", "Novak", "Kovačić", "Knežević", "Vuković", "Marković", "Petrović", "Pavlović", "Božić", "Grgić", "Perić"], []),
    "CH": (["Luca", "Noah", "Nico", "Fabian", "Silvan", "Remo", "Yann", "Dominik", "Cédric", "Ruben", "Michel", "Joël", "Jonas", "Gian", "Sandro"],
           ["Müller", "Meier", "Schmid", "Keller", "Weber", "Huber", "Steiner", "Fischer", "Gerber", "Brunner", "Baumann", "Frei", "Zimmermann", "Moser", "Widmer"], []),
    "DK": (["Mikkel", "Rasmus", "Frederik", "Jonas", "Mathias", "Kasper", "Andreas", "Christian", "Jesper", "Emil", "Magnus", "Oliver", "Viktor", "Tobias", "Anders"],
           ["Jensen", "Nielsen", "Hansen", "Pedersen", "Andersen", "Christensen", "Larsen", "Sørensen", "Rasmussen", "Jørgensen", "Madsen", "Kristensen", "Olsen", "Thomsen", "Poulsen"], []),
    "PL": (["Jakub", "Kacper", "Mateusz", "Bartosz", "Piotr", "Kamil", "Paweł", "Michał", "Szymon", "Adrian", "Krzysztof", "Łukasz", "Tomasz", "Damian", "Wojciech"],
           ["Nowak", "Kowalski", "Wiśniewski", "Wójcik", "Kamiński", "Lewandowicz", "Zieliński", "Szymański", "Woźniak", "Dąbrowski", "Kozłowski", "Jankowski", "Mazur", "Krawczyk", "Piotrowski"], []),
    "TR": (["Emre", "Burak", "Mert", "Arda", "Kerem", "Cenk", "Ozan", "Hakan", "Yusuf", "Barış", "Okay", "Ferdi", "Kaan", "Orkun", "Salih"],
           ["Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Yıldız", "Aydın", "Öztürk", "Arslan", "Doğan", "Kılıç", "Aslan", "Çetin", "Kurt", "Koç"], []),
    "AU": (["Jack", "Mitchell", "Riley", "Connor", "Harry", "Lachlan", "Jordan", "Cameron", "Nathan", "Bailey", "Kye", "Aiden", "Liam", "Jackson", "Martin"],
           ["Smith", "Jones", "Williams", "Brown", "Wilson", "Taylor", "Johnson", "White", "Martin", "Anderson", "Thompson", "Nguyen", "Walker", "Harris", "Ryan"], []),
    "EG": (["Mohamed", "Ahmed", "Mahmoud", "Omar", "Mostafa", "Karim", "Hossam", "Tarek", "Amr", "Youssef", "Ali", "Ibrahim", "Hamdi", "Emam", "Zizo"],
           ["Hassan", "Ali", "Ibrahim", "Mahmoud", "Abdelaziz", "El-Sayed", "Fathy", "Hamed", "Kamal", "Mansour", "Nasser", "Saber", "Tawfik", "Youssef", "Zaki"], []),
    "CM": (["Jean", "Samuel", "Pierre", "Eric", "Vincent", "Georges", "André", "Bryan", "Olivier", "Martin", "Karl", "Frank", "Christian", "Nicolas", "Serge"],
           ["Mbarga", "Nkoulou", "Onana", "Ekambi", "Ngadeu", "Anguissa", "Toko", "Mbeumo", "Tchamba", "Fai", "Ngamaleu", "Wooh", "Kunde", "Ebosse", "Nkoudou"], []),
    "PY": (["Miguel", "Julio", "Ángel", "Gustavo", "Ramón", "Diego", "Óscar", "Mathías", "Hernán", "Robert", "Alejandro", "Antonio", "Blas", "Junior", "Fabián"],
           ["Benítez", "Cáceres", "Villalba", "Riveros", "Ortiz", "Báez", "Giménez", "Almirón", "Cardozo", "Ojeda", "Sosa", "Galeano", "Escobar", "Aquino", "Rojas"], []),
    "SE": (["Erik", "Oscar", "Viktor", "Emil", "Alexander", "Filip", "Gustav", "Isak", "Jesper", "Hugo", "Ludvig", "Anton", "Linus", "Robin", "Albin"],
           ["Andersson", "Johansson", "Karlsson", "Nilsson", "Eriksson", "Larsson", "Olsson", "Persson", "Svensson", "Gustafsson", "Pettersson", "Jonsson", "Lindberg", "Lindqvist", "Berg"], []),
    "NO": (["Martin", "Sander", "Jonas", "Kristian", "Magnus", "Fredrik", "Mathias", "Ola", "Sindre", "Henrik", "Morten", "Eirik", "Even", "Aron", "Leo"],
           ["Hansen", "Johansen", "Olsen", "Larsen", "Andersen", "Pedersen", "Nilsen", "Kristiansen", "Jensen", "Karlsen", "Berg", "Haugen", "Hagen", "Bakken", "Strand"], []),
    "AT": (["David", "Lukas", "Florian", "Stefan", "Marcel", "Philipp", "Christoph", "Patrick", "Maximilian", "Michael", "Konrad", "Nicolas", "Romano", "Kevin", "Alexander"],
           ["Gruber", "Huber", "Bauer", "Wagner", "Pichler", "Steiner", "Moser", "Mayer", "Hofer", "Leitner", "Berger", "Fuchs", "Eder", "Fischer", "Schmid"], []),
    "IR": (["Ali", "Mehdi", "Reza", "Mohammad", "Hossein", "Saeid", "Ahmad", "Farshad", "Hamid", "Moslem", "Hamed", "Vahid", "Saman", "Morteza", "Milad"],
           ["Ahmadi", "Hosseini", "Karimi", "Rezaei", "Mohammadi", "Moradi", "Jafari", "Ebrahimi", "Rahimi", "Tavakoli", "Esmaili", "Ghasemi", "Sadeghi", "Ansari", "Taheri"], []),
    "KZ": (["Nurlan", "Arman", "Daniyar", "Yerlan", "Askar", "Bauyrzhan", "Timur", "Ruslan", "Aibek", "Serik", "Dauren", "Marat", "Azamat", "Islam", "Abylai"],
           ["Abenov", "Nurgaliyev", "Zhumabekov", "Seitkali", "Akhmetov", "Bekov", "Iskakov", "Kassymov", "Omarov", "Suleimenov", "Tokayev", "Utegenov", "Yessenov", "Zhakupov", "Mukhanov"], []),
}

# (nome, sigla, código de nomes, cor1, cor2, bandeira, padrão do uniforme principal)
NATIONS = [
    ("Argentina", "ARG", "AR", "#75aadb", "#ffffff", ["h", "#75aadb", "#ffffff", "#75aadb"], "listras"),
    ("Espanha", "ESP", "ES", "#c60b1e", "#ffc400", ["h", "#c60b1e", "#ffc400", "#ffc400", "#c60b1e"], "liso"),
    ("França", "FRA", "FR", "#1e3a8a", "#ffffff", ["v", "#002395", "#ffffff", "#ed2939"], "liso"),
    ("Inglaterra", "ING", "EN", "#ffffff", "#1e3a8a", ["cross", "#ffffff", "#ce1124"], "liso"),
    ("Brasil", "BRA", "BR", "#ffdf00", "#009c3b", ["disc", "#009c3b", "#ffdf00", "#002776"], "liso"),
    ("Portugal", "POR", "PT", "#c8102e", "#006600", ["vsplit", "#006600", "#ff0000"], "liso"),
    ("Holanda", "HOL", "NL", "#f36c21", "#111827", ["h", "#ae1c28", "#ffffff", "#21468b"], "liso"),
    ("Bélgica", "BEL", "BE", "#c8102e", "#111111", ["v", "#000000", "#fae042", "#ed2939"], "liso"),
    ("Alemanha", "ALE", "DE", "#ffffff", "#111111", ["h", "#000000", "#dd0000", "#ffce00"], "liso"),
    ("Croácia", "CRO", "HR", "#ffffff", "#c8102e", ["h", "#ff0000", "#ffffff", "#171796"], "metades"),
    ("Marrocos", "MAR", "MA", "#c1272d", "#006233", ["star", "#c1272d", "#006233"], "liso"),
    ("Itália", "ITA", "IT", "#0066b3", "#ffffff", ["v", "#009246", "#ffffff", "#ce2b37"], "liso"),
    ("Colômbia", "COL", "CO", "#fcd116", "#003893", ["h", "#fcd116", "#fcd116", "#003893", "#ce1126"], "liso"),
    ("Uruguai", "URU", "UY", "#5cbfeb", "#111111", ["h", "#ffffff", "#0038a8", "#ffffff", "#0038a8", "#ffffff"], "liso"),
    ("México", "MEX", "MX", "#006847", "#ffffff", ["v", "#006847", "#ffffff", "#ce1126"], "liso"),
    ("Japão", "JAP", "JP", "#1d2b6b", "#ffffff", ["disc", "#ffffff", "#bc002d", "#bc002d"], "liso"),
    ("Estados Unidos", "EUA", "US", "#ffffff", "#1e3a8a", ["h", "#b22234", "#ffffff", "#b22234", "#ffffff", "#b22234", "#ffffff", "#b22234"], "liso"),
    ("Senegal", "SEN", "SN", "#ffffff", "#00853f", ["v", "#00853f", "#fdef42", "#e31b23"], "liso"),
    ("Suíça", "SUI", "CH", "#d52b1e", "#ffffff", ["cross", "#d52b1e", "#ffffff"], "liso"),
    ("Dinamarca", "DIN", "DK", "#c60c30", "#ffffff", ["nordic", "#c60c30", "#ffffff"], "liso"),
    ("Coreia do Sul", "COR", "KR", "#c60c30", "#111111", ["disc", "#ffffff", "#cd2e3a", "#0047a0"], "liso"),
    ("Equador", "EQU", "EC", "#ffdd00", "#034ea2", ["h", "#ffdd00", "#ffdd00", "#034ea2", "#ed1c24"], "liso"),
    ("Áustria", "AUT", "AT", "#ed2939", "#ffffff", ["h", "#ed2939", "#ffffff", "#ed2939"], "liso"),
    ("Turquia", "TUR", "TR", "#e30a17", "#ffffff", ["star", "#e30a17", "#ffffff"], "liso"),
    ("Austrália", "AUS", "AU", "#ffcd00", "#00843d", ["star", "#00008b", "#ffffff"], "liso"),
    ("Canadá", "CAN", "CA", "#d52b1e", "#ffffff", ["v", "#d52b1e", "#ffffff", "#d52b1e"], "liso"),
    ("Ucrânia", "UCR", "UA", "#ffd500", "#005bbb", ["h", "#005bbb", "#ffd500"], "liso"),
    ("Nigéria", "NIG", "NG", "#008751", "#ffffff", ["v", "#008751", "#ffffff", "#008751"], "liso"),
    ("Polônia", "POL", "PL", "#ffffff", "#dc143c", ["h", "#ffffff", "#dc143c"], "liso"),
    ("Egito", "EGI", "EG", "#c8102e", "#ffffff", ["h", "#ce1126", "#ffffff", "#000000"], "liso"),
    ("Suécia", "SUE", "SE", "#fecc00", "#006aa7", ["nordic", "#006aa7", "#fecc00"], "liso"),
    ("Noruega", "NOR", "NO", "#ba0c2f", "#ffffff", ["nordic", "#ba0c2f", "#ffffff"], "liso"),
    ("Peru", "PER", "PE", "#ffffff", "#d91023", ["v", "#d91023", "#ffffff", "#d91023"], "faixa"),
    ("Costa do Marfim", "CIV", "CI", "#f77f00", "#009e60", ["v", "#f77f00", "#ffffff", "#009e60"], "liso"),
    ("Chile", "CHI", "CL", "#d52b1e", "#0039a6", ["h", "#ffffff", "#d52b1e"], "liso"),
    ("Paraguai", "PAR", "PY", "#d52b1e", "#ffffff", ["h", "#d52b1e", "#ffffff", "#0038a8"], "listras"),
    ("Camarões", "CMR", "CM", "#007a5e", "#ce1126", ["v", "#007a5e", "#ce1126", "#fcd116"], "liso"),
    ("Arábia Saudita", "ARS", "SA", "#ffffff", "#006c35", ["star", "#006c35", "#ffffff"], "liso"),
    ("Gana", "GAN", "GH", "#ffffff", "#111111", ["h", "#ce1126", "#fcd116", "#006b3f"], "liso"),
    ("Rússia", "RUS", "RU", "#ffffff", "#1e3a8a", ["h", "#ffffff", "#0039a6", "#d52b1e"], "liso"),
]

FUTSAL_NATIONS = ["Brasil", "Argentina", "Espanha", "Portugal", "Irã", "Marrocos", "Rússia", "Cazaquistão", "Japão", "Itália",
                  "Ucrânia", "Paraguai", "Croácia", "França", "Holanda", "Polônia", "Colômbia", "Uruguai", "Estados Unidos", "Egito"]

FUTSAL_ONLY = {
    "Irã": ("IRA", "IR", "#ffffff", "#239f40", ["h", "#239f40", "#ffffff", "#da0000"], "liso"),
    "Cazaquistão": ("CAZ", "KZ", "#00afca", "#fec50c", ["disc", "#00afca", "#fec50c", "#fec50c"], "liso"),
}
