import os
import urllib.request
import urllib.parse
import json
import time

BASE_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Archivo_Historico")

ASSETS = {
    "01_Barcos_y_Combate_Naval": [
        ("File:HuáscarValparaiso.jpg", "01_Monitor_Huascar_1879.jpg"),
        ("File:CorbetaEsmeralda.jpg", "02_Corbeta_Esmeralda_1879.jpg"),
        ("File:Combate_Naval_Iquique-Thomas_Somerscales.jpg", "03_Combate_Naval_de_Iquique_Somerscales.jpg"),
        ("File:Fragata_Cochrane.jpg", "04_Fragata_Blindada_Cochrane.jpg"),
        ("File:BlancoEncalada.jpg", "05_Blindado_Blanco_Encalada.jpg"),
        ("File:Goleta_Covadonga.jpg", "06_Goleta_Covadonga.jpg"),
        ("File:Covadonga_Punta_Gruesa.jpg", "07_Covadonga_Punta_Gruesa.jpg"),
        ("File:Fragata_independencia_perfil.jpg", "08_Fragata_Blindada_Independencia.jpg"),
        ("File:Damageshuascar.JPG", "09_Huascar_Danio_Combate_Angamos.jpg"),
        ("File:Lisle-Captura_del_Huáscar_por_el_blindado_chileno_Blanco_Encalada_y_Cochrane.png", "10_Captura_del_Huascar_Blanco_y_Cochrane.png")
    ],
    "02_Fotos_Soldados_y_Personajes": [
        ("File:Arturo_Prat.jpg", "01_Arturo_Prat_Chacon.jpg"),
        ("File:M_Grau(2).jpg", "02_Miguel_Grau_Seminario.jpg"),
        ("File:Francisco_Bolognesi.jpg", "03_Francisco_Bolognesi.jpg"),
        ("File:Andrés_Avelino_Cáceres_2.jpg", "04_Andres_Avelino_Caceres.jpg"),
        ("File:Alfonso_Ugarte.jpg", "05_Coronel_Alfonso_Ugarte.jpg"),
        ("File:Colorados_de_bolivia.jpg", "06_Batallon_Colorados_de_Bolivia.jpg"),
        ("File:Patricio_Lynch_Solo_de_Zaldivar.jpg", "07_Patricio_Lynch.jpg"),
        ("File:Cantinera_Irene_Morales(1865-1890).jpg", "08_Cantinera_Irene_Morales.jpg"),
        ("File:Enslaved_Chinese_coolie_in_Peru_1881.jpg", "09_Culi_Chino_Esclavizado_Peru_1881.jpg"),
        ("File:Soldado_Boliviano,_viste_uniforme_de_campaña.jpg", "10_Soldado_Boliviano_Uniforme_Campania.jpg"),
        ("File:Reg-civ-mov-aconcagua.jpg", "11_Soldados_Chilenos_Regimiento_Aconcagua_Comblain.jpg"),
        ("File:Mutilado-en-la-GdP-21.jpg", "12_Mutilado_de_Guerra_1880.jpg"),
        ("File:Spenser_Buckingham_St._John_-_st.jpg", "13_Spencer_St_John_Observador_Britanico.jpg")
    ],
    "03_Lugares_y_Campos_de_Batalla": [
        ("File:Desembarco_en_Pisagua.JPG", "01_Desembarco_de_Pisagua_1879.jpg"),
        ("File:Desembarco_y_toma_de_Pisagua,_1879.jpg", "02_Desembarco_y_Toma_de_Pisagua_Grabado.jpg"),
        ("File:Bombardeo_de_pisagua_18_abril_1879.JPG", "03_Bombardeo_de_Pisagua_1879.jpg"),
        ("File:Batalla_Tarapaca_Jose_Effio_1887.jpg", "04_Batalla_de_Tarapaca_Jose_Effio.jpg"),
        ("File:Tacna_Pampa_Del_Cerro_Intiorko.jpg", "05_Campo_de_la_Alianza_Tacna_Intiorko.jpg"),
        ("File:Batalla_tacna.png", "06_Batalla_de_Tacna_Grabado.png"),
        ("File:ALMORRODESDEFUERTESANJOSE.jpg", "07_Morro_de_Arica_Foto_Historica_1880.jpg"),
        ("File:Batalla_del_Morro_de_Arica.png", "08_Batalla_del_Morro_de_Arica.png"),
        ("File:El_tercer_reducto.jpg", "09_Reducto_Numero_3_Miraflores_1881.jpg"),
        ("File:Chorrillos_pared_destruida.JPG", "10_Ruinas_de_Chorrillos_1881.jpg"),
        ("File:BATALLA-DE-MIRAFLORES2.jpg", "11_Batalla_de_Miraflores_1881.jpg")
    ],
    "04_Uniformes_y_Armamento": [
        ("File:Chassepot_rifle_1866_technical_drawing_-_Bolt_assembly.jpg", "01_Fusil_Chassepot_1866_Cerrojo.jpg"),
        ("File:Chassepot_rifle_1866_technical_drawing_-_Barrel.jpg", "02_Fusil_Chassepot_1866_Canon.jpg"),
        ("File:Fusil_Gras_M80_1874_culasse.jpg", "03_Fusil_Gras_1874_Cerrojo.jpg"),
        ("File:Fusil_Gras_M80_1874_metallic_cartridge.jpg", "04_Fusil_Gras_Cartucho_Metalico.jpg"),
        ("File:Remington_Rolling_Block.jpg", "05_Fusil_Remington_Rolling_Block.jpg"),
        ("File:Alto_alianza_armamento_aliado.jpg", "06_Armamento_Aliado_Alto_Alianza.jpg"),
        ("File:Réplica_de_vestimenta_de_soldado_chileno_de_la_Guerra_del_Pacifico.jpg", "07_Uniforme_Soldado_Chileno_Replica.jpg"),
        ("File:Ilustración_soldados_chilenos_-_Guerra_del_Pacifico.JPG", "08_Ilustracion_Soldados_Chilenos.jpg")
    ],
    "05_Cartas_y_Documentos": [
        ("File:Carta_de_Grau_a_viuda_de_Prat_en_monumento_chileno.JPG", "01_Facsimil_Monumento_Carta_Grau_a_Carmela_Carvajal.jpg"),
        ("File:Carmela_Carvajal_Briones.jpg", "02_Carmela_Carvajal_Briones.jpg"),
        ("File:La_respuesta_de_Bolognesi.jpg", "03_La_Respuesta_de_Bolognesi.jpg"),
        ("File:Firma_de_Francisco_Bolognesi_en_1859.jpg", "04_Firma_Francisco_Bolognesi.jpg"),
        ("File:Plano_aricae-0001.jpg", "05_Plano_Militar_Batalla_de_Arica_1880.jpg"),
        ("File:War_of_the_Pacific_LOC_map.png", "06_Mapa_Historico_Guerra_del_Pacifico_LOC.png")
    ]
}

HEADERS = {'User-Agent': 'PacificoHistoryGame/1.0 (historical.research@game.dev)'}

def get_image_url(wiki_file):
    encoded_file = urllib.parse.quote(wiki_file)
    url = f"https://commons.wikimedia.org/w/api.php?action=query&titles={encoded_file}&prop=imageinfo&iiprop=url|size&format=json"
    req = urllib.request.Request(url, headers=HEADERS)
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            data = json.loads(resp.read().decode('utf-8'))
            pages = data.get('query', {}).get('pages', {})
            for p in pages.values():
                ii = p.get('imageinfo')
                if ii and len(ii) > 0:
                    return ii[0].get('url')
    except Exception as e:
        print(f"Error querying {wiki_file}: {e}")
    return None

def download_file(url, target_path):
    req = urllib.request.Request(url, headers=HEADERS)
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            content = resp.read()
            with open(target_path, 'wb') as f:
                f.write(content)
            return len(content)
    except Exception as e:
        print(f"Error downloading {url}: {e}")
        return 0

def main():
    os.makedirs(BASE_DIR, exist_ok=True)
    summary_results = {}
    
    for category, items in ASSETS.items():
        cat_dir = os.path.join(BASE_DIR, category)
        os.makedirs(cat_dir, exist_ok=True)
        print(f"\n==========================================")
        print(f"PROCESANDO CATEGORÍA: {category}")
        print(f"==========================================")
        
        for wiki_file, local_name in items:
            target_file = os.path.join(cat_dir, local_name)
            if os.path.exists(target_file) and os.path.getsize(target_file) > 1000:
                print(f"  [EXISTE] {local_name} ({os.path.getsize(target_file)} bytes)")
                continue
                
            print(f"  [BUSCANDO] {wiki_file}...")
            url = get_image_url(wiki_file)
            if url:
                print(f"    -> Descargando {local_name}...")
                size = download_file(url, target_file)
                if size > 0:
                    print(f"    [OK] Guardado: {local_name} ({size / 1024:.1f} KB)")
                    summary_results[local_name] = {"status": "success", "size": size, "category": category}
                else:
                    print(f"    [FALLO] No se pudo guardar {local_name}")
            else:
                print(f"    [NO ENCONTRADO] {wiki_file}")
            time.sleep(0.5)

    print("\nProceso de descarga completado.")

if __name__ == "__main__":
    main()
