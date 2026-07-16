# HART — Material técnico para el paper (IEEE Access)

*Preparado por Fran para Álvaro — respuesta al correo del [fecha]. Estructurado en 6 bloques según lo solicitado.*

---

## 1. Modelo y datos de SmartPLS

| Recurso | Enlace |
|---|---|
| Proyecto/ficheros SmartPLS | https://github.com/fcantosj/Material-Paper/tree/main/SMART-PLS |
| Base de datos (duplicada 41→82 filas para que SmartPLS pudiera calcular) | https://github.com/fcantosj/Material-Paper/blob/main/SMART-PLS/Evaluaci%C3%B3n%20HART%20(respuestas)%20-%20Copia%20de%20Evaluaci%C3%B3n.txt |
| Cuestionario (ES/EN) | https://docs.google.com/spreadsheets/d/1Q_qItcb-bt8CbyeMcVLXH_cA092uP6aOEyXKVMT4F-0/edit?usp=sharing |
| Resultados completos (hojas: Resultados ES/EN, Codificados, Percepción Personalización, Reliability+Loadings, Discriminant Validity, Validación Hipótesis) | Mismo Google Sheet de arriba |

**Análisis realizados en SmartPLS:**
- Reliability + Loadings → **PLS-SEM Algorithm**
- Validación de hipótesis → **Bootstrapping**

**Codificación de variables (constructos y sus ítems):**

| Constructo | Ítems |
|---|---|
| COMP (Comparación/Preferencia versión 2) | COMP1, COMP2, COMP3, COMP4 |
| PP (Personalización Percibida) | PP1, PP2 |
| PL (Aprendizaje Percibido) | PL1, PL2, PL3, PL4 |
| EOU (Facilidad de Uso) | EOU1, EOU2, EOU3 |
| GE (Experiencia General/Gamificación) | GE1, GE2, GE3 |
| IM (Inmersión) | IM1, IM2 |
| US (Usabilidad/Satisfacción) | US1, US2, US3 |
| RI (Intención de Revisita) | RI1, RI2 |

*(El texto exacto de cada ítem está en el cuestionario enlazado arriba.)*

> **[PENDIENTE]** Confirmar si hace falta adjuntar capturas de pantalla de los outputs de SmartPLS (HTMT, path coefficients) o si con el Google Sheet os basta para recalcular.

---

## 2. Detalles técnicos del sistema y del RAG

### Stack de modelos

| Componente | Modelo/Herramienta |
|---|---|
| STT | Whisper (ONNX) para Unity — [inference-engine-whisper-tiny](https://huggingface.co/unity/inference-engine-whisper-tiny) |
| LLM clasificador / chat / generación de preguntas | Gemini Flash `[PENDIENTE confirmar versión exacta: doc dice 3.5, código usa google/gemini-flash-latest]` |
| LLM audioguía | Claude Sonnet 4.6 (vía OpenRouter, `anthropic/claude-sonnet-4-6`) |
| Embeddings | `[PENDIENTE resolver inconsistencia — ver nota abajo]` |
| TTS | `[PENDIENTE resolver inconsistencia — ver nota abajo]` |

> ⚠️ **Nota — embeddings y TTS:** el documento fuente original indica embeddings "llama-text-embed-v2" y TTS "Kokoro-TTS + ElevenLabs (pregenerados)", pero el código de producción actual (`agent.py`) usa **Ollama `nomic-embed-text`** para embeddings y **edge-tts** para TTS. Hay que confirmar cuál stack corresponde exactamente a la **versión evaluada por los 31 usuarios** del estudio, porque puede que el sistema haya cambiado entre la evaluación y ahora.

### Base de conocimiento (RAG)

| Parámetro | Valor |
|---|---|
| Vector DB | Pinecone |
| Artistas | 8 (Velázquez, Van Gogh, Monet, Kandinsky, Koons, Dalí, Munch, Picasso) |
| Fragmentos/chunks indexados | 1.363 |
| Tamaño de chunk | 150 |
| Overlap | 1.000 |
| Top-K de recuperación | 5 |
| Idioma del corpus y respuestas | Español `[PENDIENTE confirmar si el estudio se hizo solo en ES, dado que el sistema es bilingüe ES/EN]` |
| Manejo de contexto insuficiente | No contemplado — no hay fallback específico si Pinecone no recupera contexto relevante |

**Obras por artista (namespace de Pinecone):**

- **Van Gogh:** La noche estrellada, El dormitorio en Arlés, Almendro en flor, Los girasoles, Trigal con cuervos
- **Munch:** El Grito
- **Monet:** Impresión Sol Naciente, Mujer con sombrilla, El jardín del artista en Giverny, Puente japonés, Los nenúfares
- **Dalí:** La persistencia de la memoria, El gran masturbador, Los elefantes
- **Kandinsky:** Composición 8
- **Picasso:** Guernica, Las señoritas de Avignon, Autorretrato 1907, Dora Maar Au Chat, El viejo y el nuevo año
- **Velázquez:** Las Meninas, La fábula de Aracne, Retrato de un hombre
- **Koons:** Balloon Dog

**Fuentes usadas para construir la base de conocimiento:**
historia-arte.com, Van Gogh Museum, Wikipedia (ES), Museo del Prado, Museo Thyssen, Guggenheim Bilbao, Museo Reina Sofía, jeffkoons.com — lista completa de URLs disponible en el documento fuente.

---

## 3. Latencias del sistema

Medidas sobre **61 interacciones reales**, ejecutadas en el **ordenador de la HoloWall** (entorno de producción, no en local del desarrollador).

| Etapa | Latencia media |
|---|---|
| STT | 0,7 s |
| Clasificador (CHAT/AUDIOGUIDE) | 3,21 s |
| RAG (retrieval Pinecone) | 6,17 s |
| Generación LLM | 10,32 s |
| TTS | 3,84 s |
| **Total (pregunta → reproducción)** | **22,15 s** |

> **[PENDIENTE]** Si el paper necesita justificar "responsive", puede convenir añadir specs del hardware de la HoloWall (CPU/GPU/RAM) para contextualizar estos tiempos.

---

## 4. Ejemplos reales de adaptación de preguntas

> ⚠️ Los tres ejemplos siguientes muestran la pregunta, la respuesta de MUSA y el **resultado final** de `update_questions`. Falta el estado de las preguntas **antes** de la actualización (lo que devolvía `get_questions` en ese turno) — recomendable recuperarlo de `agent.log` o del histórico de `questions.json` en Git si Álvaro necesita el contraste completo antes/después.

### Ejemplo 1 — Nivel: beginner (Picasso)
**Pregunta del usuario:** "¿Cuál es la obra más famosa de Picasso y de qué trata?"

**Respuesta de MUSA:** La obra más famosa de Picasso es, sin duda, el Guernica, una de las grandes joyas que preside nuestra sala. Este monumental cuadro retrata el dolor y la devastación causados por el bombardeo de la villa vasca de Guernica durante la Guerra Civil Española. A través de figuras desgarradoras, como una madre que llora a su hijo o animales heridos en medio del caos, Picasso creó un grito universal contra la crueldad de la guerra que aún hoy nos conmueve profundamente.

**Flujo de herramientas:** `retrieve_context → speak → get_questions → update_questions`

**Motivo de la actualización:** el usuario preguntó específicamente por Guernica, tema no cubierto en detalle en las preguntas beginner previas.

**Preguntas resultantes (nivel beginner):**
1. ¿Cuál es la obra más famosa de Picasso según la explicación? → *El Guernica* (distractores: Las Meninas, La persistencia, El grito)
2. ¿Qué conflicto histórico inspiró la creación de la obra Guernica? → *La Guerra Civil*
3. ¿Qué animal sufriente aparece representado junto al toro en Guernica? → *Un caballo herido*
4. ¿En qué tonos está pintado el caos en el Guernica? → *Gris, negro, blanco*

---

### Ejemplo 2 — Nivel: hard (Picasso)
**Pregunta del usuario (transcripción con errores fonéticos de "Las señoritas de Aviñón"):** "¿Qué me puedes contar de la obra de las señoras pintadas de rosa en la pared derecha?"

**Respuesta de MUSA:** Te refieres a Las señoritas de Avignon, ese revolucionario lienzo de la pared derecha que cambió el rumbo del arte moderno. En él, Picasso retrata a cinco mujeres con cuerpos descompuestos en planos angulares y rostros que emulan máscaras africanas y esculturas íberas. Esta obra rompió por completo con las reglas de la perspectiva tradicional, marcando el nacimiento del cubismo y desafiando la concepción clásica de la belleza.

**Flujo de herramientas:** `retrieve_context → speak → get_questions → update_questions`

**Motivo de la actualización:** demuestra la robustez del sistema ante transcripción fonética imperfecta — MUSA identificó la obra correctamente pese al error de STT, y las preguntas de nivel "hard" (Comprender/Aplicar, según Bloom) se generaron centradas en el cubismo y la ruptura con la perspectiva clásica.

**Preguntas resultantes (nivel hard):**
1. ¿Por qué Picasso usó máscaras africanas en Las señoritas de Avignon? → *Desafiar la belleza tradicional*
2. ¿Cómo se rompe la perspectiva tradicional en este lienzo? → *Geometrizando rostros en planos*
3. ¿Qué relación tienen los cuerpos descompuestos con el arte moderno? → *Iniciaron el movimiento cubista*
4. ¿En qué se diferencian estas figuras de la belleza clásica? → *Planos angulares y máscaras*

---

### Ejemplo 3 — Nivel: expert (Picasso)
**Pregunta del usuario:** "¿Cuál es un dato curioso de Picasso?"

**Respuesta de MUSA:** ¿Sabías que Picasso es considerado uno de los artistas más prolíficos de todos los tiempos? Se calcula que a lo largo de su vida llegó a crear alrededor de trece mil quinientas pinturas, cien mil grabados y unas trescientas esculturas. Su pasión por el arte era tan desbordante que podía dar vida a una obra maestra en cuestión de segundos, dejando una huella imborrable y un legado gigantesco.

**Flujo de herramientas:** `retrieve_context → speak → get_questions → update_questions`

**Motivo de la actualización:** tema (prolificidad/productividad artística) no cubierto en preguntas expert previas; nivel expert exige razonamiento de tipo Analizar/Evaluar (Bloom), de ahí preguntas del tipo "¿qué implica...?" / "¿qué interpretación...?".

**Preguntas resultantes (nivel expert):**
1. ¿Qué infiere la creación de obras maestras en segundos sobre Picasso? → *Su virtuosismo técnico instantáneo*
2. ¿Qué interpretación es coherente con su masiva producción de grabados? → *Un proceso creativo compulsivo*
3. Si priorizó la pintura, ¿qué implica de su legado escultórico? → *La pintura dominó su [obra]*
4. ¿Cuál causa explica mejor su gigantesco volumen de obras creadas? → *Un impulso creativo ininterrumpido*

---

## 5. Funcionamiento de la generación de trivia

**Decisión de actualizar preguntas:**
Tras generar la audioguía, el agente orquestador (LangGraph) llama primero a `get_questions` para recuperar las preguntas actuales del nivel activo. Solo llama a `update_questions` si el prompt determina que la explicación acaba de introducir **temas no cubiertos aún** por las preguntas existentes — instrucción explícita en el prompt del sistema: *"llámala (a la herramienta de actualizar preguntas) SOLO si lo que acabas de explicar introduce temas no cubiertos aún"*.

**Control de dificultad:**
El nivel activo del usuario en la sala (`beginner` / `hard` / `expert`) se pasa directamente al prompt de generación y determina tanto la taxonomía de Bloom aplicada como el umbral de similitud semántica de los distractores.

| Nivel | Taxonomía de Bloom | Similitud semántica del distractor |
|---|---|---|
| beginner | Recordar (¿Cuál…? ¿Dónde…? ¿En qué año…?) | < 0.3 |
| hard | Comprender/Aplicar (¿Por qué…? ¿Qué relación…?) | 0.3 – 0.6 |
| expert | Analizar/Evaluar (¿Qué implica…? ¿Qué interpretación…?) | > 0.6 |

**Validación de calidad de las preguntas:**
- No existe ningún mecanismo automático para detectar o filtrar preguntas ambiguas o incorrectas.
- No se valida la corrección de la respuesta marcada como correcta ni la plausibilidad de los distractores — se confía enteramente en la salida estructurada del LLM (`QuestionBatch`, vía `with_structured_output`).

> Esto es relevante como **limitación** a mencionar en el paper: la calidad del contenido generado depende de la fiabilidad del LLM sin validación posterior.

---

## 6. Protocolo experimental con usuarios

| Aspecto | Detalle |
|---|---|
| Orden de condiciones | Todos los usuarios hicieron primero **baseline (sinPP)** y después **personalización (conPP)** — **no se contrabalanceó** el orden |
| Duración | ~20 minutos por sesión |
| Preguntas/interacciones por usuario | 32 en total (16 baseline + 16 personalizadas) — mitad del recorrido en cada condición, todo en modo fácil |
| Artistas/obras | Todos los usuarios pasaron por los mismos 8 artistas, pero **en orden distinto** para cada participante |
| Perfil de participantes | N=41 (22 mujeres, 19 hombres); 6 personas de fuera de STEM, el resto de la ETSIT |
| Consentimiento | Consentimiento informado + política de privacidad (RGPD, UE 2016/679) — se recoge únicamente el nombre, almacenado separado de las respuestas; anonimización mediante código (P01, P02…); eliminación del nombre a los 12 meses |
| Aprobación ética formal | `[PENDIENTE — confirmar si existe aprobación de comité de ética institucional o si el estudio se acogió a exención por bajo riesgo; el documento solo recoge consentimiento informado]` |
| Rango de edad | `[PENDIENTE — no recogido explícitamente en el material actual]` |

### Guion de instrucciones dado a los participantes (resumen)
1. **Bienvenida:** explicación de que es un estudio sobre experiencia de museo holográfico con guía virtual (MUSA) y juego de preguntas.
2. **Procedimiento:** dos visitas idénticas en recorrido y obras, pero con el sistema funcionando de forma distinta en cada una (no se revela la diferencia hasta el final, para no sesgar la experiencia).
3. **Durante cada sala:** MUSA da una explicación del artista; el usuario puede hacerle preguntas libremente, como a un guía humano; tras la explicación aparece el juego de trivia.
4. **Tras cada juego:** pregunta corta de personalización percibida (escala 1–5) sobre si las preguntas se sintieron relacionadas con la conversación con MUSA.
5. **Al final:** cuestionario completo sobre la experiencia global.

---

## Resumen de pendientes (`[PENDIENTE]`)

- [ ] Confirmar stack real de **embeddings** y **TTS** usado durante el estudio evaluado (vs. stack actual del repo).
- [ ] Confirmar versión exacta del modelo Gemini Flash usado en el estudio.
- [ ] Confirmar si el estudio se realizó únicamente en español.
- [ ] Añadir specs de hardware de la HoloWall (opcional, refuerza el claim de "responsive").
- [ ] Recuperar el estado "antes" de las preguntas de trivia en los 3 ejemplos (desde `agent.log` o histórico de `questions.json`).
- [ ] Confirmar aprobación ética formal (comité de ética / exención).
- [ ] Añadir rango de edad de los participantes.