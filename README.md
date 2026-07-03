# 📦 Nombre del Módulo: BusCheckInV2

---

## 🧭 Propósito

`BusCheckInV2` es una aplicación móvil Android (**.NET MAUI 9**, package ID `com.mrlucky.buscheckinv2`, versión 2.2.2) diseñada para que los **choferes de transporte de GAB/MrLucky** registren digitalmente el inicio, los pasajeros embarcados y el cierre de cada viaje de flete. Reemplaza un proceso manual de conteo de empleados, generando un registro digital trazable en un servidor SQL Server central. Opera con una estrategia **offline-first**: persiste todos los datos localmente en SQLite y los sincroniza con la API REST del servidor en segundo plano cada vez que hay conectividad disponible.

---

## ⚙️ Responsabilidades

- Gestionar el ciclo de vida completo de un **flete de personal**: creación, escaneo de pasajeros, finalización y sincronización.
- Autoadministrar un catálogo local (proveedores, rutas) sembrado directamente en SQLite desde código, sin sincronización remota del catálogo.
- Registrar cada pasajero mediante **escaneo de código de barras Code39** (número de nómina), capturando también coordenadas GPS y timestamp.
- Insertar registros centinela de **INICIO** (CveNomina = 0) y **FIN** (CveNomina = 9999) en cada viaje para delimitar el trayecto.
- Sincronizar los registros pendientes con la API REST de forma automática cada 15 segundos y también bajo demanda (al detectar red o al intentar salir de la pantalla de escaneo).
- Bloquear la navegación de retroceso cuando existen registros sin sincronizar, presentando al usuario la opción de reintentar o salir a riesgo.
- Detectar y aplicar actualizaciones de la propia APK via HTTP desde un servidor interno, comparando `versionCode` del dispositivo con un archivo `version.txt`.
- Persistir el estado del último flete activo en `Android Preferences` para permitir la continuación del viaje si la app se cierra inesperadamente.
- Registrar cada intento de sincronización en una tabla local `Tb_Sync_Log` para diagnóstico, limpiando entradas mayores a 7 días automáticamente.

---

## 🔄 Flujo de Funcionamiento

### Pantalla 1 — Selección de Flete (`SeleccionDeFlete`)

1. Al arrancar (`App.OnStart`), `SQLiteService.InitializeAsync` crea las tablas y ejecuta `SeedDataAsync`, insertando proveedores y rutas hardcodeadas si las tablas están vacías.
2. En `OnAppearing`, el ViewModel verifica actualizaciones de la APK vía HTTP. Si hay una versión mayor, presenta un diálogo de **actualización obligatoria** que descarga e instala el APK desde la URL interna.
3. Se verifica si existe un flete activo guardado en `Preferences` (`ultimo_flete_local_id`). Si existe y los proveedores ya están cargados (segunda sesión), se ofrece al chofer continuar el viaje anterior.
4. El chofer selecciona: **Proveedor** → **Ruta** (filtrada por proveedor y estado "A") → **Tipo de Flete** → **Tipo de Viaje**, y escribe su nombre.
5. Al presionar **Continuar**, se valida que los cinco campos estén completos. Se crea un registro `Tb_FlePer_FletePersonal` en SQLite local (estado `IsSynced = false`), y se guardan las claves del flete activo en `Preferences`.
6. La navegación avanza a `EscaneoCodigo` pasando el `fleteLocalId` como query parameter de Shell.

### Pantalla 2 — Escaneo de Código (`EscaneoCodigo`)

7. `EscaneoCodigoViewModel.InitializeAsync` carga los pasajeros ya existentes en SQLite para ese flete (reanudación), inserta el sentinela **INICIO** si no existe, y arranca el daemon de sincronización en background.
8. Se suscribe a cambios de conectividad (`Connectivity.ConnectivityChanged`): al recuperar red, dispara sincronización inmediata.
9. El chofer activa la cámara con el botón **Escanear** (toggle). El componente `CameraView` detecta códigos Code39.
10. Por cada código detectado: se valida que sea numérico, que el número de nómina no esté ya en la lista, se captura GPS (best-effort), se reproduce un beep, se inserta el registro en SQLite y se verifica la persistencia releyendo la tabla.
11. Si hay conexión, se dispara `TrySyncDataAsync` (protegido por `SemaphoreSlim` para evitar ejecuciones concurrentes).
12. La sincronización sigue dos pasos: (a) sincronizar el **flete padre** (`InsertarFletePersonal`) para obtener el `IdFletePer` del servidor, y (b) sincronizar cada **detalle pendiente** (`InsertarDetFlete`, `InsertarInicioDetFlete`, `InsertarFinDetFlete`).
13. Si la navegación de retroceso se detecta mientras hay pendientes sin sincronizar, se cancela y se muestra un `ActionSheet` con las opciones: Quedarme aquí / Reintentar sincronización / Salir de todas formas.
14. Al finalizar el viaje, se inserta el sentinela **FIN** (CveNomina = 9999), se actualiza la cantidad de pasajeros en el registro padre, se intenta una sincronización final y se llama a `/UpdateFletePersonal` para actualizar la cantidad en el servidor. Se limpian las `Preferences`.

### Pantalla 3 — Fletes Pendientes (`FletesPendientes`)

15. Pantalla de consulta que lista fletes por chofer, con filtros por días y estado (solo pendientes). Accesible desde la toolbar de `SeleccionDeFlete`. Permite finalizar, cancelar o reanudar fletes directamente desde la API del servidor.

---

## 📐 Reglas de Negocio

### 🔒 Restricciones

- **R1 — Todos los campos son obligatorios en la creación del flete.** El sistema bloquea el avance si alguno de los cinco campos (nombre del chofer, proveedor, ruta, tipo de flete, tipo de viaje) está vacío o sin seleccionar.
- **R2 — Solo rutas activas son visibles.** Al seleccionar un proveedor, el sistema filtra las rutas por la relación `Tb_FlePer_ProvRuta` y solo muestra las que tienen `RutaStatus = "A"`.
- **R3 — Un pasajero no puede ser escaneado dos veces en el mismo viaje.** La validación compara el número de nómina contra la colección en memoria; si ya existe un registro con ese `CveNomina` y `Nombre == null` (registro regular), se rechaza con alerta de sonido y mensaje.
- **R4 — Solo se aceptan códigos completamente numéricos.** El patrón `^[0-9]+$` rechaza cualquier valor que contenga letras u otros caracteres.
- **R5 — La UI no se actualiza si la inserción en SQLite falla.** Si `InsertAsync` lanza excepción, se muestra alerta con instrucción de reescanear; el registro NO aparece en pantalla hasta que la inserción sea exitosa.
- **R6 — La salida de la pantalla de escaneo está bloqueada con pendientes de sync.** El evento `Shell.Navigating` cancela el pop/popToRoot e informa al chofer la cantidad de registros pendientes.

### ✅ Validaciones

- **V1 — Código de barras numérico puro:** `Regex.IsMatch(valor, @"^[0-9]+$")` + `int.TryParse`.
- **V2 — Nómina no duplicada:** búsqueda en `ObservableCollection<Tb_FlePer_DetFlete>` por `CveNomina == nominaCapturada && Nombre == null`.
- **V3 — FleteLocalId válido en EscaneoCodigo:** si `_fleteLocalId == 0`, se detiene la inicialización y se muestra alerta.
- **V4 — Verificación post-insert:** tras `InsertAsync`, se relee la tabla para confirmar que el registro existe. Si no, se emite un `Console.WriteLine` de advertencia pero no se bloquea al usuario.
- **V5 — Validación de actualización de APK:** se compara `versionCode` remoto (entero) con el del dispositivo; si el remoto es mayor, la actualización se trata como obligatoria.
- **V6 — Flete padre antes de detalles:** en `SincronizarDetallesAsync`, si `IdFletePer` del flete local es null o 0, la sincronización de detalles se aborta.
- **V7 — FlePer_Nombre obligatorio en el backend:** el servidor rechaza con 400 si el campo es null. La app envía "INICIO", "FIN" o "(pendiente)" como placeholder; el backend sobreescribe el nombre de empleados regulares desde `tb_cat_empleados` usando `CveNomina`.

### 🔁 Agrupaciones

- **G1 — Tipos de Flete:** `NORMAL`, `MIXTO`, `T.E.`, `EXTRAORDINARIO` (lista hardcodeada en el ViewModel).
- **G2 — Tipos de Viaje:** `TRAER GENTE`, `LLEVAR GENTE` (lista hardcodeada en el ViewModel).
- **G3 — Registros especiales:** CveNomina = 0 → INICIO de viaje; CveNomina = 9999 → FIN de viaje; cualquier otro valor → pasajero regular.
- **G4 — Conteo de pasajeros:** se cuentan únicamente los registros donde `CveNomina != 0 AND CveNomina != 9999`.
- **G5 — Estado de flete (EstadoCalculado):** valores posibles: `"Activo"`, `"En curso"`, `"Pendiente"`, `"Finalizado"`, `"Cancelado"`. Los tres primeros se consideran `EsPendiente = true`.
- **G6 — Proveedores iniciales:** `TURISTICOS` (Juan Carlos Acosta), `10810` (Rivera Montesino), `RAMIROGE` (Ramiro García). Sembrados en primera instalación.

### ⚙️ Reglas Operativas

- **O1 — Sincronización automática cada 15 segundos** cuando la app está en la pantalla de escaneo, hay conexión y existen registros pendientes.
- **O2 — Sincronización disparada inmediatamente** al recuperar conectividad de red o tras cada escaneo exitoso si hay conexión.
- **O3 — Solo un proceso de sincronización simultáneo** (protegido por `SemaphoreSlim(1,1)` y bandera `_isSyncInProgress`).
- **O4 — Daemon de sync se cancela** al hacer `Dispose` del ViewModel (navegación fuera de la pantalla), vía `CancellationTokenSource`.
- **O5 — INICIO se auto-inserta al entrar a la pantalla de escaneo** si no existe ningún registro con `CveNomina = 0` para ese flete local.
- **O6 — FIN se inserta al presionar "Finalizar Viaje"** si no existe ningún registro con `CveNomina = 9999`. No es posible finalizar sin confirmación del chofer.
- **O7 — El log de sincronización se limpia** a 7 días de retención, en cada ciclo de `InitializeAsync` de `SeleccionDeFleteViewModel`.
- **O8 — La camera se deshabilita durante la sincronización** (`CanScan = !IsSyncing`) para liberar recursos del hardware.
- **O9 — Política de reintentos Polly** aplicada al `HttpClient` de `ApiFleteService` (no al de `AppUpdateService`, para que los 404 de versión no generen reintentos innecesarios).
- **O10 — Actualización de APK es obligatoria** según el texto del diálogo ("Actualización Obligatoria"), aunque el código permite continuar si el usuario pulsa "Cancelar" en la segunda rama condicional.

---

## 🔗 Dependencias

**NuGet / Bibliotecas:**

- `BarcodeScanning.Native.Maui` — escaneo de códigos de barras Code39 via cámara nativa Android.
- `CommunityToolkit.Maui` — `Popup`, `Toast`, `ShowPopup`, `AppThemeBinding` y otras utilidades MAUI.
- `CommunityToolkit.Mvvm` — `ObservableObject`, `ObservableProperty`, `RelayCommand`, generación de código MVVM.
- `Plugin.Maui.Audio` — reproducción de audios `scanner.mp3` y `error.mp3`.
- `Polly` + `Polly.Extensions.Http` — política de reintentos HTTP para `ApiFleteService`.
- `sqlite-net-pcl` — ORM ligero para SQLite local con soporte async, WAL y `SharedCache`.
- `Newtonsoft.Json` — deserialización en `AppUpdateService` (lectura de `version.txt`).
- `System.Text.Json` — serialización/deserialización en `ApiFleteService`.
- `Microsoft.Extensions.Logging` — logging estructurado en `SQLiteService` y `ApiFleteService`.
- `Microsoft.Maui.Networking` — `Connectivity.Current.NetworkAccess` para detección de red.
- `Microsoft.Maui.Devices.Sensors` — `Geolocation.GetLocationAsync` para captura de coordenadas GPS.

**Servicios internos:**

- `IApiFleteService / ApiFleteService` — cliente HTTP hacia la API REST del servidor.
- `ISQLiteService / SQLiteService` — capa de acceso a SQLite local.
- `IAlertService / AlertService` — abstracción de `DisplayAlert`, `DisplayActionSheet`, `DisplayPromptAsync`.
- `INavigationService / NavigationService` — abstracción de `Shell.Current.GoToAsync`.
- `IAudioService / AudioService` — reproducción de beeps de confirmación y error.
- `IAppUpdateService / AppUpdateService` — detección y descarga de actualizaciones de APK.
- `IVersionService / VersionServiceAndroid` — lectura de `VersionString` y `BuildNumber` desde Android.

**Endpoints de la API REST:**

- Base URL: `http://189.206.160.206:82/BusCheckInV2/api/WSBusCheckInV2`
- `GET  /ObtenerUsuarios`
- `GET  /ObtenerFletesPorChofer?chofer=&dias=&soloPendientes=`
- `POST /ValidarYFinalizarFlete`
- `POST /CancelarFlete`
- `POST /ReanudarFlete`
- `POST /SincronizarFletes`
- `GET  /VerificarConexion`
- `POST /InsertarFletePersonal`
- `POST /InsertarDetFlete`
- `POST /InsertarInicioDetFlete`
- `POST /InsertarFinDetFlete`
- `POST /UpdateFletePersonal`
- `POST /SincronizarDetFletes` (batch, más eficiente pero aún no reemplazó al individual)
- `GET  http://189.206.160.206:81/EmbarquesApk/BusCheckInV2/version.txt` (actualización APK)

**Base de datos local (SQLite):**

- `Tb_FlePer_FletePersonal` — flete cabecera.
- `Tb_FlePer_DetFlete` — detalles (pasajeros, INICIO, FIN).
- `Tb_Cat_Proveedor` — catálogo de proveedores (sembrado).
- `Tb_FlePer_Ruta` — catálogo de rutas (sembrado).
- `Tb_FlePer_ProvRuta` — relación proveedor–ruta.
- `Tb_Sync_Log` — log de sincronizaciones.

---

## ⚠️ Riesgos Técnicos

**RT1 — IP pública hardcodeada sin TLS.**
`ApiConstants.BaseUrlDebug == ApiConstants.BaseUrlRelease == "http://189.206.160.206:82"`. Ambos entornos apuntan al mismo servidor con HTTP plano. Cualquier observador en la red puede interceptar nóminas, coordenadas GPS y datos de transporte. El manifiesto Android también declara `android:usesCleartextTraffic="true"`. Riesgo crítico de seguridad.

**RT2 — Credenciales del keystore expuestas en el .csproj.**
`AndroidSigningStorePass`, `AndroidSigningKeyAlias` y `AndroidSigningKeyPass` tienen el valor `"GABIRA"` en texto plano dentro del archivo versionado. Cualquier persona con acceso al repositorio puede firmar APKs como si fuera la organización.

**RT3 — Catálogo de proveedores y rutas hardcodeado.**
`SeedDataAsync` inserta datos de proveedores y rutas directamente desde el código fuente. Si el negocio agrega o modifica proveedores o rutas en el servidor, la app no los reflejará hasta que se lance una nueva versión. No existe mecanismo de sincronización del catálogo.

**RT4 — Dos librerías de serialización JSON coexistentes.**
`ApiFleteService` usa `System.Text.Json`; `AppUpdateService` usa `Newtonsoft.Json`. Esto aumenta el tamaño del APK y puede generar comportamientos inconsistentes ante casos de deserialización edge.

**RT5 — `GetItemsAsync<T>()` carga tablas completas en memoria.**
No existe paginación ni filtrado en la consulta base. En dispositivos con muchos viajes acumulados, la carga de `Tb_FlePer_DetFlete` completa puede causar lentitud o `OutOfMemoryException`, especialmente en hardware con poca RAM.

**RT6 — Método de verificación post-insert innecesariamente costoso.**
Tras cada escaneo, `ProcessBarcodeValueAsync` inserta el registro y luego recarga TODA la tabla `Tb_FlePer_DetFlete` para verificar la existencia. Esto duplica las operaciones de I/O por cada pasajero escaneado.

**RT7 — `MainPage.xaml` / `MainPage.xaml.cs` son artefactos del scaffold sin usar.**
Estos archivos heredados del template inicial de MAUI no participan en la navegación de la app (que usa Shell con `AppShell`). Su presencia puede generar confusión en mantenimiento futuro.

**RT8 — Acoplamiento directo a `Application.Current.MainPage` en ViewModel.**
`SeleccionDeFleteViewModel` llama directamente a `Application.Current.MainPage.DisplayAlert(...)` en lugar de usar la abstracción `IAlertService` ya disponible. Esto rompe la separación MVVM y dificulta pruebas unitarias.

**RT9 — Método `ContinuarAsyncLEGACY` presente en producción.**
El ViewModel contiene una implementación legacy del flujo de creación de flete, comentada pero activa en el código. Su presencia indica un refactor incompleto.

**RT10 — La actualización de APK descarga sin verificación de integridad.**
`AppUpdateService` descarga el APK vía HTTP y lo instala directamente sin validar checksum ni firma del archivo descargado. Un ataque MITM podría inyectar código malicioso.

---

## 🧪 Casos Edge

- **GPS no disponible:** el sistema captura coordenadas `(0.0, 0.0)` como fallback. Los registros con esas coordenadas son indistinguibles de un escaneo en la ubicación geográfica `null island`.
- **App cerrada entre INICIO y primer escaneo:** `Preferences` guarda el `fleteLocalId`; al reabrir, el sistema detecta el flete y ofrece continuar. El sentinela INICIO ya fue insertado, por lo que no se duplica.
- **Sincronización parcialmente exitosa:** si el flete padre sube pero algunos detalles fallan, el siguiente ciclo de sync intenta solo los detalles pendientes. El sistema es resiliente a fallos parciales.
- **Nómina = 0 o 9999 escaneada desde código de barras real:** el sistema no filtra estos valores explícitamente en `ValidarCodigo`. Un empleado con nómina 0 o 9999 podría ser confundido con los sentinelas INICIO/FIN.
- **Mismo proveedor sin rutas activas:** el picker de rutas queda vacío e `IsRutaEnabled = false`, bloqueando la creación del flete sin un mensaje que explique que el problema es del catálogo.
- **Flete en `Preferences` cuya BD fue borrada:** el sistema lo detecta (`GetItemAsync` devuelve null), limpia las `Preferences` y continúa normalmente.
- **Dos instancias del sync disparadas desde hilos distintos:** la bandera `_isSyncInProgress` y el `SemaphoreSlim` protegen la ejecución; la segunda instancia sale inmediatamente.
- **Backend con `FlePer_Nombre = null`:** produce 400 BadRequest. El fix implementado envía `"(pendiente)"` como placeholder, el cual es sobreescrito por el backend. Si el backend no aplica el CONCAT, el campo queda como `"(pendiente)"` en SQL Server.
- **`CanScan` en false durante sync + usuario pulsa Escanear:** el botón está deshabilitado, pero si el binding falla por algún motivo de timing, el scan se protege adicionalmente por `_isProcessingBarcode`.

---

## 🧱 Suposiciones Detectadas

- **S1:** El servidor en `189.206.160.206:82` es accesible desde la red interna donde operan los dispositivos. La app no tiene lógica de descubrimiento dinámico de host.
- **S2:** Los dispositivos Android tienen API Level ≥ 21 (Android 5.0 Lollipop).
- **S3:** La cámara trasera del dispositivo puede leer códigos Code39 sin procesamiento adicional.
- **S4:** El catálogo de proveedores y rutas no cambia con frecuencia suficiente como para requerir sincronización dinámica.
- **S5:** Los números de nómina son enteros de 32 bits (`int`), sin soporte para valores negativos o que superen `int.MaxValue`.
- **S6:** El campo `FlePer_Nombre` del servidor es decorativo para registros regulares de empleados; el nombre real siempre se resuelve desde `tb_cat_empleados` por `CveNomina` en el SQL del backend.
- **S7:** Un único chofer opera la app en un dispositivo a la vez (no hay autenticación, el nombre del chofer se ingresa manualmente sin validación de credenciales).
- **S8:** La app solo requiere distribución Android; el código iOS en `AppUpdateService` es un stub no funcional (`itms-apps://itunes.apple.com/app/idTU_APP_ID`).
- **S9:** El archivo `version.txt` del servidor siempre es JSON válido con las claves `versionCode` y `downloadURL`.
- **S10:** `IAudioService` es un singleton de larga vida; disposearlo desde el ViewModel causaría errores en usos posteriores, por lo que el Dispose del ViewModel deliberadamente no lo libera.

---

## 📈 Recomendaciones Técnicas

**REC1 — Migrar API a HTTPS con certificado válido (Prioridad Alta).**
Reemplazar `http://189.206.160.206:82` por un endpoint HTTPS, remover `android:usesCleartextTraffic="true"` del manifiesto, y eliminar la `HttpClientHandler` sin validación de certificado. Sin esto, las nóminas de empleados y coordenadas GPS viajan en texto plano.

**REC2 — Extraer credenciales del keystore a variables de entorno o GitHub Secrets (Prioridad Alta).**
Eliminar `AndroidSigningStorePass`, `AndroidSigningKeyAlias` y `AndroidSigningKeyPass` del `.csproj` y pasarlos como variables de entorno en el pipeline CI/CD o en un archivo `.env` excluido del repositorio.

**REC3 — Implementar sincronización dinámica del catálogo (Prioridad Media).**
Agregar un endpoint `/ObtenerCatalogo` (o equivalente) que retorne proveedores y rutas activas, y llamarlo en `InitializeAsync` cuando haya conectividad. El `SeedDataAsync` actual solo debería actuar como fallback de primera instalación.

**REC4 — Unificar la serialización JSON en `System.Text.Json` (Prioridad Baja).**
Eliminar `Newtonsoft.Json` de `AppUpdateService` y reemplazarlo por `System.Text.Json.JsonDocument` o un DTO tipado, reduciendo el tamaño del APK y la deuda técnica de dependencias.

**REC5 — Reemplazar la verificación post-insert por retorno del ID autogenerado (Prioridad Media).**
`sqlite-net-pcl` pobla el campo `[PrimaryKey, AutoIncrement]` en el objeto después de `InsertAsync`. Usar `registro.Id > 0` como verificación en lugar de recargar toda la tabla tras cada escaneo.

**REC6 — Filtrar detalles de flete por `FleteLocalId` en la consulta SQLite (Prioridad Media).**
Sustituir `GetItemsAsync<Tb_FlePer_DetFlete>()` (carga total) por una consulta con predicado `d => d.FleteLocalId == _fleteLocalId` para evitar cargar todos los viajes históricos en memoria.

**REC7 — Eliminar artefactos no usados: `MainPage.xaml`, `ContinuarAsyncLEGACY` (Prioridad Baja).**
Reducir la superficie de confusión para futuros desarrolladores eliminando código inactivo.

**REC8 — Mover los `DisplayAlert` del ViewModel a `IAlertService` (Prioridad Media).**
`SeleccionDeFleteViewModel` llama directamente a `Application.Current.MainPage.DisplayAlert`. Usar `IAlertService` que ya está inyectado en otros ViewModels para mantener la separación MVVM y facilitar pruebas unitarias.

**REC9 — Agregar validación de integridad al proceso de actualización de APK (Prioridad Alta).**
Incluir un hash SHA-256 en `version.txt` y verificar el archivo descargado antes de instalarlo. La descarga actual via HTTP sin verificación es un vector de ataque de suplantación.

**REC10 — Filtrar nóminas 0 y 9999 de la validación de duplicados (Prioridad Media).**
Asegurar que `ValidarCodigo` rechace explícitamente los valores reservados para sentinelas (0 y 9999) si provienen del escáner, para evitar que un código de barras físico corrompa el conteo del viaje.

---

## 🧾 Resumen Ejecutivo

`BusCheckInV2` es la herramienta digital que usan los **choferes de GAB/MrLucky** para registrar con precisión cuántos empleados abordan cada unidad de transporte en cada viaje. Antes de salir, el chofer selecciona en la app a qué proveedor de transporte pertenece, qué ruta va a recorrer, y si el viaje es de traslado o de regreso. Luego escanea con la cámara del teléfono el código de identificación de cada empleado que sube, y al terminar el recorrido confirma el cierre del viaje.

Todo queda guardado primero en el propio teléfono (para que funcione sin internet), y se envía automáticamente al servidor central en cuanto hay señal. Si el chofer intenta salir de la pantalla de escaneo con datos aún no enviados, la app le avisa y le da la opción de reintentar antes de perder la información.

El sistema también se actualiza a sí mismo: si la empresa publica una versión nueva, el chofer verá un aviso la próxima vez que abra la app y la actualización se instala directamente desde el servidor interno.

Los principales aspectos a atender antes de considerar el sistema completamente seguro y robusto son: (1) la comunicación con el servidor ocurre sin cifrado, exponiendo datos de empleados en la red; (2) las contraseñas de firma de la app están visibles en el código fuente; y (3) el catálogo de proveedores y rutas no se actualiza automáticamente desde el servidor, por lo que cualquier cambio operativo requiere una nueva versión de la app.