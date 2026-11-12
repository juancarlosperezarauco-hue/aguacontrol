package bo.aquacontrol.movil

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.text.InputType
import android.view.View
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.*
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.core.content.FileProvider
import androidx.core.view.ViewCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.*
import org.json.JSONArray
import org.json.JSONObject
import java.io.ByteArrayOutputStream
import java.io.File
import java.time.OffsetDateTime
import java.time.ZoneId

class MainActivity: AppCompatActivity() {
 private val padding by lazy { dp(18) }
 private val preferences by lazy { getSharedPreferences("aquacontrol_mobile",MODE_PRIVATE) }
 private lateinit var api: ApiClient
 private lateinit var session: Session
 private var screenJob: Job?=null
 private var mapView: WebView?=null
 private var photoOrder: Int?=null
 private var photoActivity: Int?=null
 private var cameraUri: Uri?=null
 private var mapPayload=JSONObject()
 private val locationPermission=registerForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { result ->
  if(result[Manifest.permission.ACCESS_FINE_LOCATION]==true||result[Manifest.permission.ACCESS_COARSE_LOCATION]==true)startSharing() else message("Sin permiso no se comparte ubicación. Puede consultar sus trabajos.")
 }
 private val notificationsPermission=registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted -> if(granted)startSharing() else message("Autorice las notificaciones para mantener visible el control de la jornada.") }
 private val photoPicker=registerForActivityResult(ActivityResultContracts.GetContent()) { uri -> if(uri!=null)uploadPhoto(uri) }
 private val camera=registerForActivityResult(ActivityResultContracts.TakePicture()) { success -> if(success)cameraUri?.let(::uploadPhoto) }

 override fun onCreate(savedInstanceState: Bundle?){
  super.onCreate(savedInstanceState)
  WindowCompat.setDecorFitsSystemWindows(window,false)
  photoOrder=savedInstanceState?.getInt("photoOrder")?.takeIf { it>0 }
  photoActivity=savedInstanceState?.getInt("photoActivity")?.takeIf { it>0 }
  cameraUri=savedInstanceState?.getString("cameraUri")?.let(Uri::parse)
  val existing=FieldRuntime.api
  if(existing!=null){api=existing;lifecycleScope.launch{try{session=api.me();home()}catch(_:Exception){showLogin()}}}else showLogin()
 }
 override fun onSaveInstanceState(out: Bundle){super.onSaveInstanceState(out);out.putInt("photoOrder",photoOrder?:0);out.putInt("photoActivity",photoActivity?:0);out.putString("cameraUri",cameraUri?.toString())}
 private fun home(){if(session.mustChangePassword){changePassword();return};if("field.monitor" in session.permissions)showMonitor() else if("field.share" in session.permissions)showOrders() else {showLogin();message("Este perfil no tiene acceso móvil de campo ni monitoreo.")}}
 private fun showLogin(){
  val content=vertical();content.addView(title("AquaControl Móvil"));content.addView(text("Ingrese con la cuenta creada por su administrador."))
  val server=field("Dirección del servidor",preferences.getString("server","http://10.0.2.2:5080").orEmpty())
  val user=field("Usuario");val password=field("Contraseña",input=InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD)
  val status=text("");val enter=button("Ingresar"){
   if(server.text.isBlank()||user.text.isBlank()||password.text.isBlank()){status.text="Complete todos los campos";return@button}
   lifecycleScope.launch {
    try{
     status.text="Conectando…";api=ApiClient(server.text.toString());session=api.login(user.text.toString(),password.text.toString())
     FieldRuntime.api=api;preferences.edit().putString("server",server.text.toString()).apply();password.text.clear();home()
    }catch(e:Exception){status.text=e.message}
   }
  }
  listOf(server,user,password,enter,status).forEach(content::addView);screen(content)
 }
 private fun changePassword(){
  val content=vertical();content.addView(title("Cambiar contraseña temporal"));content.addView(text("Use al menos 12 caracteres con letras y números."))
  val current=field("Contraseña actual",input=InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD)
  val next=field("Nueva contraseña",input=InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_PASSWORD)
  content.addView(current);content.addView(next);content.addView(button("Guardar y volver a ingresar"){lifecycleScope.launch{try{api.changePassword(current.text.toString(),next.text.toString());FieldRuntime.api=null;showLogin()}catch(e:Exception){message(e.message)}}});screen(content)
 }
 private fun showOrders(){
  val content=vertical();val reader="orders.readings" in session.permissions
  content.addView(title(if(reader)"Lecturas y recibos" else "Trabajos técnicos"));content.addView(text(session.name))
  val tracking=text(FieldRuntime.state.value.message);content.addView(tracking)
  content.addView(button("Iniciar jornada y compartir ubicación"){explainTracking()})
  content.addView(button("Finalizar jornada / dejar de compartir"){stopSharing()})
  content.addView(text("Mis puntos en el orden guardado por el supervisor. La línea azul sigue la red de vías SIG. Use Navegar para indicaciones giro a giro."))
  val map=createMap();content.addView(map,LinearLayout.LayoutParams(-1,dp(340)))
  val status=text("Consultando mis trabajos…");val list=vertical();content.addView(status)
  content.addView(button("Actualizar trabajos"){lifecycleScope.launch{loadOrders(list,status)}});content.addView(list);content.addView(button("Cerrar sesión"){logout()});screen(content)
  screenJob=lifecycleScope.launch {
   launch{FieldRuntime.state.collect { tracking.text=it.message;if(it.latitude!=null&&it.longitude!=null)map.evaluateJavascript("window.setPosition(${it.latitude},${it.longitude})",null) }}
   while(isActive){loadOrders(list,status);delay(20_000)}
  }
 }
 private suspend fun loadOrders(list: LinearLayout,status: TextView){
  try{
   val routes=api.routes();val route=routes.firstOrNull();val stops=route?.optJSONArray("stops")?.objects().orEmpty()
   val sequence=stops.associate{it.getJSONObject("order").getInt("id") to it.getInt("sequence")}
   val orders=api.orders().filter{it.status !in listOf("CERRADA","CANCELADA","VERIFICADA","FINALIZADA","NO_REALIZADA")}.sortedWith(compareBy<WorkOrder>{sequence[it.id]?:Int.MAX_VALUE}.thenBy{it.scheduledAt})
   val array=JSONArray();list.removeAllViews()
   orders.forEachIndexed{i,o->val stop=stops.firstOrNull{it.getJSONObject("order").getInt("id")==o.id};val index=sequence[o.id]?.toString()?:"Sin ruta"
    array.put(JSONObject().put("id",o.id).put("sequence",sequence[o.id]?:0).put("label",o.number).put("latitude",o.latitude).put("longitude",o.longitude).put("status",o.status))
    list.addView(button("$index. ${stop?.optString("client")?:o.number} · ${stop?.optString("connectionCode").orEmpty()}\n${o.number} · ${o.status}\n${localDate(o.scheduledAt)} · ${o.priority}"+(if(stop!=null)" · tramo ${stop.optDouble("distanceMeters").toInt()} m" else "")){showOrder(o.id)})}
   if(orders.isEmpty())list.addView(text("No hay trabajos asignados. El supervisor debe asignarlos desde AquaControl."))
   updateMap(JSONObject().put("kind","route").put("points",array).put("path",route?.optJSONArray("path")?:JSONArray()));status.text="${orders.size} trabajos · ${if(route!=null)"ruta #${route.getInt("id")}" else "sin ruta guardada"} · actualización cada 20 s"
  }catch(e:Exception){status.text=e.message}
 }
 private fun showOrder(id: Int){
  val loading=vertical();loading.addView(title("Cargando trabajo…"));screen(loading)
  lifecycleScope.launch{try{renderOrder(api.orderDetail(id),api.context(id))}catch(e:Exception){loading.addView(text(e.message?:"No se pudo abrir"));loading.addView(button("Volver"){home()})}}
 }
 private fun renderOrder(detail: OrderDetail,context: JSONObject){
  val o=detail.order;val content=vertical();val type=context.getJSONObject("type").optString("code")
  content.addView(title(o.number));content.addView(text(context.getJSONObject("type").optString("name")))
  content.addView(text("${o.status} · ${o.priority}\n${localDate(o.scheduledAt)}"))
  content.addView(text("Titular: ${context.optJSONObject("client")?.optString("name")?:"Trabajo de área"}"))
  content.addView(text("Servicio: ${context.optJSONObject("connection")?.optString("code")?:"Sin conexión"}\nDirección: ${context.optJSONObject("connection")?.optString("address").orEmpty()}\nMedidor: ${context.optJSONObject("meter")?.optString("serial")?:"Sin medidor instalado"}"))
  content.addView(text("Código fijo: ${if(context.isNull("fixedCode"))"No vinculado" else context.optString("fixedCode")}"))
  content.addView(text(o.instructions))
  val gps=FieldRuntime.state.value
  content.addView(text("Destino: ${o.latitude}, ${o.longitude}\n"+(if(gps.latitude!=null&&gps.longitude!=null){val result=FloatArray(1);android.location.Location.distanceBetween(gps.latitude,gps.longitude,o.latitude,o.longitude,result);"Mi última posición enviada: ${gps.latitude}, ${gps.longitude}\nDistancia directa al servicio: ${result[0].toInt()} m · precisión ±${gps.accuracy?.toInt()} m"}else "Inicie jornada y active GPS para ver su posición y la distancia al destino.")))
  content.addView(button("Obtener GPS / iniciar jornada"){explainTracking()})
  content.addView(button("Navegar hasta este punto"){navigate(o)})
  val next=when(o.status){"ASIGNADA"->"EN_CAMINO" to "Iniciar traslado";"EN_CAMINO"->"EN_EJECUCION" to "Comenzar trabajo";else->null}
  next?.let{content.addView(button(it.second){changeState(o,it.first)})}
  if(o.status=="EN_CAMINO")content.addView(button("Marcar no realizado (requiere visita GPS)"){changeState(o,"NO_REALIZADA")})
  if(o.status in listOf("EN_CAMINO","EN_EJECUCION"))content.addView(button("Guardar visita con GPS, medidor y lectura"){visit(o,context)})
  if(o.status=="EN_EJECUCION"){
   if(type=="LECTURA"&&"readings.write" in session.permissions){content.addView(button("Registrar lectura del medidor"){reading(o,context)});content.addView(button("Emitir recibo de la lectura"){invoice(o,context)})}
   if(type in listOf("INSTALACION","CAMBIO_MEDIDOR","NUEVA_CONEXION")&&"meters.install" in session.permissions)content.addView(button("Registrar instalación / cambio"){install(o,context)})
   if(o.effect=="CORTE"&&!o.effectDone)content.addView(button("Validar corte en línea (60 segundos)"){perform(o.id){api.post("/orders/${o.id}/authorize-cut",JSONObject().put("version",o.version));message("Validado. Deténgase si el supervisor cancela; registre el efecto después del corte físico.")}})
   if(o.effect in listOf("CORTE","RECONEXION")&&!o.effectDone)content.addView(button("Confirmar operación física realizada"){AlertDialog.Builder(this).setTitle("¿Ya realizó la operación autorizada?").setMessage("Confirme únicamente después de actuar sobre este medidor.").setNegativeButton("Cancelar",null).setPositiveButton("Sí, registrar"){_,_->perform(o.id){api.post("/orders/${o.id}/effect",JSONObject().put("version",o.version))}}.show()})
  }
  content.addView(title("Actividades"))
  detail.activities.forEach{a->
   content.addView(text("${if(a.done)"✓" else "○"} ${a.name}${if(a.evidenceRequired)" · evidencia requerida" else ""}"))
   if(!a.done&&o.status=="EN_EJECUCION")content.addView(button("Registrar resultado"){val input=field("Observaciones",a.result);AlertDialog.Builder(this).setTitle(a.name).setView(input).setNegativeButton("Cancelar",null).setPositiveButton("Guardar"){_,_->perform(o.id){api.updateActivity(o.id,a,input.text.toString())}}.show()})
  }
  val evidence=context.getJSONArray("evidence").objects()
  content.addView(title("Fotos y evidencias"));content.addView(text("Fotografíe el medidor y el servicio terminado. No incluya personas ni documentos ajenos."))
  evidence.forEach{e->content.addView(text("✓ ${e.optString("fileName")} · Medidor ${e.optString("meterSerial").ifBlank{"Sin serie registrada"}}"))}
  context.optJSONArray("visits")?.objects()?.forEach{v->content.addView(text("Visita ${v.optString("outcome")} · ${localDate(v.optString("receivedAt"))}\nMedidor ${v.optString("observedSerial")} · lectura ${if(v.isNull("reading"))"—" else v.opt("reading")} · a ${v.optDouble("distanceMeters").toInt()} m del servicio\n${v.optString("observation")}"))}
  if(o.status=="EN_EJECUCION"){
   content.addView(button("Tomar o adjuntar fotografía"){choosePhoto(o.id,detail.activities)})
   content.addView(button("Finalizar trabajo"){
    if(evidence.none{it.optString("contentType").startsWith("image/")})message("Primero debe subir una foto del servicio/medidor") else changeState(o,"FINALIZADA")
   })
   content.addView(button("Marcar no realizado"){changeState(o,"NO_REALIZADA")})
  }
  content.addView(button("Volver a mis trabajos"){showOrders()});screen(content)
 }
 private fun navigate(o: WorkOrder){
  AlertDialog.Builder(this).setTitle("Modo de navegación").setItems(arrayOf("A pie","Moto (según cobertura de Google Maps)","Vehículo")){_,choice->
   val mode=listOf("walking","two-wheeler","driving")[choice]
   val uri=Uri.parse("https://www.google.com/maps/dir/?api=1&destination=${o.latitude},${o.longitude}&travelmode=$mode&dir_action=navigate")
   try{startActivity(Intent(Intent.ACTION_VIEW,uri))}catch(_:Exception){message("No hay aplicación de mapas disponible")}
  }.show()
 }
 private fun visit(o: WorkOrder,c: JSONObject){
  val layout=vertical();layout.addView(text("Primero suba una foto. REALIZADO exige número y lectura del medidor actual. OBSERVADO exige motivo y GPS junto al servicio. Registre el resultado antes de confirmar el efecto físico o finalizar."))
  val outcomes=Spinner(this).apply{adapter=ArrayAdapter(this@MainActivity,android.R.layout.simple_spinner_dropdown_item,listOf("REALIZADO","OBSERVADO"))}
  val serial=field("Número observado del medidor");val reading=field("Lectura actual del medidor",input=InputType.TYPE_CLASS_NUMBER or InputType.TYPE_NUMBER_FLAG_DECIMAL);val note=field("Observación / motivo si no fue realizado")
  val photos=c.getJSONArray("evidence").objects().filter{it.optString("contentType").startsWith("image/")}
  val photo=Spinner(this).apply{adapter=ArrayAdapter(this@MainActivity,android.R.layout.simple_spinner_dropdown_item,listOf("Seleccionar fotografía…")+photos.map{it.optString("fileName")+" · "+it.optString("meterSerial")})}
  listOf(outcomes,serial,reading,note,photo).forEach(layout::addView)
  val dialog=AlertDialog.Builder(this).setTitle("Guardar visita de campo").setView(ScrollView(this).apply{addView(layout)}).setNegativeButton("Cancelar",null).setPositiveButton("Guardar con GPS",null).create()
  val requestId=java.util.UUID.randomUUID().toString()
  dialog.setOnShowListener{dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener{
   val gps=FieldRuntime.state.value;val shift=FieldRuntime.shiftId;val captured=gps.capturedAt
   if(!gps.active||shift==null||gps.latitude==null||gps.longitude==null||gps.accuracy==null||captured==null){message("Inicie jornada y espere una posición GPS enviada");return@setOnClickListener}
   if(java.time.Duration.between(java.time.Instant.parse(captured),java.time.Instant.now()).seconds>60){message("GPS antiguo. Espere una posición nueva antes de guardar.");return@setOnClickListener}
   val number=reading.text.toString().replace(',','.').toDoubleOrNull();val evidence=photos.getOrNull(photo.selectedItemPosition-1)
   dialog.getButton(AlertDialog.BUTTON_POSITIVE).isEnabled=false
   lifecycleScope.launch{try{
    api.post("/orders/${o.id}/visits",JSONObject().put("requestId",requestId).put("shiftId",shift).put("outcome",outcomes.selectedItem.toString()).put("observation",note.text.toString()).put("observedSerial",serial.text.toString()).put("reading",number?:JSONObject.NULL).put("evidenceId",evidence?.getInt("id")?:JSONObject.NULL).put("latitude",gps.latitude).put("longitude",gps.longitude).put("accuracy",gps.accuracy).put("capturedAt",captured).put("version",o.version))
    dialog.dismiss();message("Visita registrada con fecha, hora y GPS");showOrder(o.id)
   }catch(e:Exception){message(e.message);dialog.getButton(AlertDialog.BUTTON_POSITIVE).isEnabled=true}}
  }};dialog.show()
 }
 private fun changeState(o: WorkOrder,next: String){val input=field("Observaciones / resultado");AlertDialog.Builder(this).setTitle(next.replace('_',' ')).setView(input).setNegativeButton("Cancelar",null).setPositiveButton("Confirmar"){_,_->perform(o.id){api.transition(o,next,input.text.toString())}}.show()}
 private fun reading(o: WorkOrder,c: JSONObject){
  if(c.isNull("installationId")){message("Registre primero un medidor instalado");return}
  val fields=vertical();fields.addView(text("Medidor ${c.optJSONObject("meter")?.optString("serial")} · lectura anterior: ${c.opt("previousReading")}"))
  val assignedPeriod=o.instructions.split('|').getOrNull(1)?.takeIf{it.matches(Regex("\\d{4}-\\d{2}"))}?:java.time.LocalDate.now().toString().take(7)
  val period=field("Período AAAA-MM",assignedPeriod);val value=field("Lectura actual",input=InputType.TYPE_CLASS_NUMBER or InputType.TYPE_NUMBER_FLAG_DECIMAL)
  fields.addView(period);fields.addView(value)
  AlertDialog.Builder(this).setTitle("Registrar consumo").setView(fields).setNegativeButton("Cancelar",null).setPositiveButton("Guardar lectura"){_,_->
   val number=value.text.toString().replace(',','.').toDoubleOrNull();if(number==null){message("Lectura inválida");return@setPositiveButton}
   perform(o.id){api.post("/readings",JSONObject().put("installationId",c.getInt("installationId")).put("period",period.text.toString()).put("value",number).put("estimated",false).put("note","Lectura en campo · ${o.number}"))}
  }.show()
 }
 private fun invoice(o: WorkOrder,c: JSONObject){
  val rows=c.getJSONArray("readings").objects();if(rows.isEmpty()){message("Primero registre la lectura de consumo");return}
  AlertDialog.Builder(this).setTitle("Lectura para emitir recibo").setItems(rows.map{it.optString("period")+" · "+it.optDouble("value")}.toTypedArray()){_,index->
   perform(o.id){val result=JSONObject(api.post("/invoices",JSONObject().put("contractId",o.contractId).put("readingId",rows[index].getInt("id")).put("dueAt",java.time.Instant.now().plusSeconds(15*86400).toString())));AlertDialog.Builder(this).setTitle(result.optString("number")).setMessage("Consumo: ${result.optDouble("consumption")} m³\nTotal: Bs ${result.optDouble("total")}\nRecibo de cobro interno registrado.").setPositiveButton("Aceptar",null).show()}
  }.show()
 }
 private fun install(o: WorkOrder,c: JSONObject){
  val meters=c.getJSONArray("availableMeters").objects();if(meters.isEmpty()){message("El administrador debe registrar medidores disponibles");return}
  val fields=vertical();val select=Spinner(this).apply{adapter=ArrayAdapter(this@MainActivity,android.R.layout.simple_spinner_dropdown_item,meters.map{it.optString("serial")})}
  val initial=field("Lectura inicial",input=InputType.TYPE_CLASS_NUMBER or InputType.TYPE_NUMBER_FLAG_DECIMAL);val final=field("Lectura final del anterior (si hay reemplazo)",input=InputType.TYPE_CLASS_NUMBER or InputType.TYPE_NUMBER_FLAG_DECIMAL)
  fields.addView(select);fields.addView(initial);fields.addView(final)
  AlertDialog.Builder(this).setTitle("Instalar medidor").setView(fields).setNegativeButton("Cancelar",null).setPositiveButton("Registrar"){_,_->
   val value=initial.text.toString().replace(',','.').toDoubleOrNull();if(value==null){message("Indique lectura inicial válida");return@setPositiveButton}
   perform(o.id){api.post("/installations",JSONObject().put("connectionId",o.connectionId).put("meterId",meters[select.selectedItemPosition].getInt("id")).put("initial",value).put("final",final.text.toString().replace(',','.').toDoubleOrNull()?:JSONObject.NULL))}
  }.show()
 }
 private fun choosePhoto(id: Int,activities: List<OrderActivity>){
  val options=listOf("Foto general del servicio")+activities.map{it.name}
  AlertDialog.Builder(this).setTitle("Vincular fotografía a").setItems(options.toTypedArray()){_,choice->
   photoOrder=id;photoActivity=if(choice==0)null else activities[choice-1].id
   AlertDialog.Builder(this).setTitle("Fotografía de evidencia").setItems(arrayOf("Tomar foto con cámara","Seleccionar foto")){_,source->
    if(source==0){val folder=File(cacheDir,"photos").apply{mkdirs()};val file=File.createTempFile("evidencia-",".jpg",folder);cameraUri=FileProvider.getUriForFile(this,packageName+".files",file);camera.launch(cameraUri!!)}else photoPicker.launch("image/*")
   }.show()
  }.show()
 }
 private fun uploadPhoto(uri: Uri){val id=photoOrder?:return;val activity=photoActivity
  lifecycleScope.launch{try{
   val bytes=withContext(Dispatchers.IO){
    val bounds=BitmapFactory.Options().apply{inJustDecodeBounds=true};contentResolver.openInputStream(uri).use{BitmapFactory.decodeStream(it,null,bounds)}
    require(bounds.outWidth>0&&bounds.outHeight>0){"Seleccione una imagen válida"}
    var sample=1;while(bounds.outWidth/sample>1800||bounds.outHeight/sample>1800)sample*=2
    val bitmap=contentResolver.openInputStream(uri).use{BitmapFactory.decodeStream(it,null,BitmapFactory.Options().apply{inSampleSize=sample})}?:error("No se pudo leer la foto")
    val output=ByteArrayOutputStream();bitmap.compress(Bitmap.CompressFormat.JPEG,85,output);bitmap.recycle();output.toByteArray()
   }
   api.uploadPhoto(id,bytes,activity);message("Fotografía guardada y vinculada al servicio");showOrder(id)
  }catch(e:Exception){message(e.message)}}
 }
 private fun showMonitor(){
  val content=vertical();content.addView(title("Supervisión de campo"));content.addView(text(session.name+" · ubicación compartida por los operarios"))
  val map=createMap();content.addView(map,LinearLayout.LayoutParams(-1,dp(430)));val status=text("");val list=vertical();content.addView(status);content.addView(list);content.addView(button("Cerrar sesión"){logout()});screen(content)
  screenJob=lifecycleScope.launch{while(isActive){try{
   val d=api.getObject("/field/positions");val rows=d.getJSONArray("operators").objects();updateMap(JSONObject().put("kind","monitor").put("points",d.getJSONArray("operators")))
   status.text="Actualización cada 10 s · ${rows.count{it.optBoolean("isLive")}} operarios con señal reciente"
   list.removeAllViews();if(rows.isEmpty())list.addView(text("No hay cuentas de operario. Cree las cuentas y asigne sus roles desde la web."))
   rows.forEach{u->list.addView(text("${u.optString("name")} · ${u.optString("role")}\n${u.optString("status").replace('_',' ')} · último envío: ${if(u.isNull("receivedAt"))"—" else localDate(u.optString("receivedAt"))}"))}
  }catch(e:Exception){status.text="Sin actualización: ${e.message}";updateMap(JSONObject().put("kind","monitor").put("points",JSONArray()))};delay(10_000)}}
 }
 private fun explainTracking(){
  if(FieldRuntime.state.value.active){message("Ya tiene una jornada activa");return}
  AlertDialog.Builder(this).setTitle("Iniciar jornada de campo").setMessage("Durante la jornada, AquaControl compartirá su ubicación con supervisores y administradores, incluso al minimizar la app. Verá una notificación y podrá detenerla al finalizar. La precisión depende del GPS y de Internet.").setNegativeButton("Cancelar",null).setPositiveButton("Continuar"){_,_->
   if(ContextCompat.checkSelfPermission(this,Manifest.permission.ACCESS_COARSE_LOCATION)!=PackageManager.PERMISSION_GRANTED)locationPermission.launch(arrayOf(Manifest.permission.ACCESS_COARSE_LOCATION,Manifest.permission.ACCESS_FINE_LOCATION))else startSharing()
  }.show()
 }
 private fun startSharing(){lifecycleScope.launch{try{
  if(FieldRuntime.state.value.active)return@launch
  if(Build.VERSION.SDK_INT>=33&&ContextCompat.checkSelfPermission(this@MainActivity,Manifest.permission.POST_NOTIFICATIONS)!=PackageManager.PERMISSION_GRANTED){notificationsPermission.launch(Manifest.permission.POST_NOTIFICATIONS);return@launch}
  val client=api;FieldRuntime.api=client;val shift=client.startShift();if(FieldRuntime.api!==client){runCatching{client.endShift(shift)};return@launch};FieldRuntime.shiftId=shift;ContextCompat.startForegroundService(this@MainActivity,Intent(this@MainActivity,FieldLocationService::class.java))
 }catch(e:Exception){message(e.message)}}}
 private fun stopSharing(): Job {val shift=FieldRuntime.shiftId;stopService(Intent(this,FieldLocationService::class.java));FieldRuntime.state.value=TrackingState()
  return lifecycleScope.launch{if(shift!=null)try{api.endShift(shift);message("Jornada finalizada")}catch(_:Exception){message("GPS detenido. Sin conexión, la última posición se marcará sin señal.")}}
 }
 private fun logout(){val stopped=stopSharing();lifecycleScope.launch{stopped.join();try{api.logout()}catch(_:Exception){};FieldRuntime.api=null;showLogin()}}
 private fun perform(id: Int,action: suspend ()->Unit){lifecycleScope.launch{try{action();showOrder(id)}catch(e:Exception){message(e.message)}}}
 private fun createMap(): WebView {
  mapView?.destroy();val view=WebView(this);mapView=view
  view.settings.javaScriptEnabled=true;view.settings.allowFileAccess=false;view.settings.allowContentAccess=false
  view.settings.domStorageEnabled=false
  view.webViewClient=object:WebViewClient(){override fun onPageFinished(view: WebView,url: String){view.evaluateJavascript("window.setData($mapPayload)",null)};override fun shouldOverrideUrlLoading(view: WebView,request: android.webkit.WebResourceRequest)=true}
  view.loadUrl("file:///android_asset/route.html");return view
 }
 private fun updateMap(data: JSONObject){mapPayload=data;mapView?.evaluateJavascript("window.setData($data)",null)}
 private fun screen(content: View){screenJob?.cancel();val root=ScrollView(this).apply{setBackgroundColor(android.graphics.Color.rgb(244,248,247));addView(content)};ViewCompat.setOnApplyWindowInsetsListener(root){view,insets->val bars=insets.getInsets(WindowInsetsCompat.Type.systemBars() or WindowInsetsCompat.Type.ime());view.setPadding(bars.left,bars.top,bars.right,bars.bottom);insets};setContentView(root)}
 private fun vertical()=LinearLayout(this).apply{orientation=LinearLayout.VERTICAL;setPadding(padding,padding,padding,padding)}
 private fun title(value: String)=TextView(this).apply{text=value;textSize=23f;setTextColor(android.graphics.Color.rgb(16,63,70));setPadding(0,dp(10),0,dp(12))}
 private fun text(value: String)=TextView(this).apply{text=value;textSize=14f;setPadding(0,dp(6),0,dp(10))}
 private fun field(label: String,value: String="",input: Int=InputType.TYPE_CLASS_TEXT)=EditText(this).apply{hint=label;setText(value);inputType=input;setPadding(dp(12),dp(12),dp(12),dp(12))}
 private fun button(label: String,action: ()->Unit)=Button(this).apply{text=label;isAllCaps=false;minHeight=dp(48);setOnClickListener{action()};layoutParams=LinearLayout.LayoutParams(-1,-2).apply{bottomMargin=dp(8)}}
 private fun dp(value: Int)=(value*resources.displayMetrics.density).toInt()
 private fun localDate(value: String)=runCatching{OffsetDateTime.parse(value).atZoneSameInstant(ZoneId.systemDefault()).format(java.time.format.DateTimeFormatter.ofPattern("dd/MM HH:mm"))}.getOrDefault(value)
 private fun message(value: String?)=Toast.makeText(this,value?:"No se pudo completar la operación",Toast.LENGTH_LONG).show()
 override fun onDestroy(){screenJob?.cancel();mapView?.destroy();super.onDestroy()}
}
