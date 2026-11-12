package bo.aquacontrol.movil

import android.Manifest
import android.app.*
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.location.Location
import android.location.LocationListener
import android.location.LocationManager
import android.os.*
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import java.time.Instant

data class TrackingState(val active: Boolean=false,val message: String="Jornada detenida",val latitude: Double?=null,val longitude: Double?=null,val accuracy: Double?=null,val capturedAt: String?=null)
object FieldRuntime {
 var api: ApiClient?=null
 var shiftId: Int?=null
 val state=MutableStateFlow(TrackingState())
}

class FieldLocationService: Service(),LocationListener {
 private val scope=CoroutineScope(SupervisorJob()+Dispatchers.Main)
 private lateinit var locations: LocationManager
 private var lastSent=0L
 private var sending=false
 private var stopping=false
 override fun onBind(intent: Intent?)=null
 override fun onCreate(){super.onCreate();locations=getSystemService(LOCATION_SERVICE) as LocationManager}
 override fun onStartCommand(intent: Intent?,flags: Int,startId: Int): Int {
  if(intent?.action=="STOP"){finishShift();return START_NOT_STICKY}
  if(FieldRuntime.api==null||FieldRuntime.shiftId==null){stopSelf();return START_NOT_STICKY}
  try {
   val manager=getSystemService(NOTIFICATION_SERVICE) as NotificationManager
   manager.createNotificationChannel(NotificationChannel("field","Jornada de campo",NotificationManager.IMPORTANCE_LOW))
   val open=PendingIntent.getActivity(this,0,Intent(this,MainActivity::class.java),PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
   val stop=PendingIntent.getService(this,1,Intent(this,FieldLocationService::class.java).setAction("STOP"),PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
   val notification=NotificationCompat.Builder(this,"field").setSmallIcon(android.R.drawable.ic_menu_mylocation)
    .setContentTitle("AquaControl · Jornada activa").setContentText("Compartiendo ubicación con supervisión")
    .setContentIntent(open).setOngoing(true).addAction(android.R.drawable.ic_media_pause,"Finalizar jornada",stop).build()
   if(Build.VERSION.SDK_INT>=29)startForeground(41,notification,ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION) else startForeground(41,notification)
   if(ContextCompat.checkSelfPermission(this,Manifest.permission.ACCESS_COARSE_LOCATION)!=PackageManager.PERMISSION_GRANTED&&ContextCompat.checkSelfPermission(this,Manifest.permission.ACCESS_FINE_LOCATION)!=PackageManager.PERMISSION_GRANTED)throw SecurityException("Permiso de ubicación no disponible")
   FieldRuntime.state.value=TrackingState(true,"Esperando una posición GPS nueva…")
   var providers=0
   for(provider in listOf(LocationManager.GPS_PROVIDER,LocationManager.NETWORK_PROVIDER))if(locations.isProviderEnabled(provider)){
    try{locations.requestLocationUpdates(provider,10_000L,0f,this,Looper.getMainLooper());providers++}catch(_:SecurityException){ }
   }
   if(providers==0)throw IllegalStateException("Active la ubicación del teléfono y vuelva a iniciar jornada")
  }catch(error: Exception){FieldRuntime.state.value=TrackingState(false,error.message?:"No se pudo activar ubicación");finishShift()}
  return START_NOT_STICKY
 }
 override fun onLocationChanged(location: Location){
  if(stopping||sending||SystemClock.elapsedRealtime()-lastSent<10_000L)return
  if(location.accuracy>1000){FieldRuntime.state.value=FieldRuntime.state.value.copy(message="Precisión insuficiente. Active ubicación precisa/GPS para enviar una posición útil.");return}
  if(System.currentTimeMillis()-location.time>120_000)return
  val api=FieldRuntime.api?:return;val shift=FieldRuntime.shiftId?:return
  sending=true;lastSent=SystemClock.elapsedRealtime()
  scope.launch {
   try {
    api.position(shift,location.latitude,location.longitude,location.accuracy.toDouble(),Instant.ofEpochMilli(location.time).toString())
    FieldRuntime.state.value=TrackingState(true,"Ubicación enviada · precisión ±${location.accuracy.toInt()} m",location.latitude,location.longitude,location.accuracy.toDouble(),Instant.ofEpochMilli(location.time).toString())
   }catch(error: Exception){
    FieldRuntime.state.value=FieldRuntime.state.value.copy(message="Sin envío: ${error.message}")
    if(error is ApiException&&error.code in listOf(401,403))finishShift()
   }finally{sending=false}
  }
 }
 override fun onProviderDisabled(provider: String){FieldRuntime.state.value=FieldRuntime.state.value.copy(message="GPS desactivado. Supervisión verá la última ubicación, no una posición en vivo.")}
 override fun onProviderEnabled(provider: String){FieldRuntime.state.value=FieldRuntime.state.value.copy(message="Esperando una posición nueva de $provider…")}
 @Deprecated("Compatibilidad Android 8-10")
 override fun onStatusChanged(provider: String?,status: Int,extras: Bundle?) { }
 private fun finishShift(){
  if(stopping)return;stopping=true;locations.removeUpdates(this)
  val api=FieldRuntime.api;val shift=FieldRuntime.shiftId
  FieldRuntime.shiftId=null;FieldRuntime.state.value=TrackingState(false,"Jornada detenida. No se envía ubicación.")
  scope.launch {
   try{if(api!=null&&shift!=null)api.endShift(shift)}catch(_: Exception){FieldRuntime.state.value=TrackingState(false,"GPS detenido. Cierre sin conexión: supervisión marcará la última ubicación como sin señal.")}
   finally{stopForeground(STOP_FOREGROUND_REMOVE);stopSelf()}
  }
 }
 override fun onDestroy(){locations.removeUpdates(this);FieldRuntime.state.value=TrackingState(false,"Seguimiento detenido");FieldRuntime.shiftId=null;scope.cancel();super.onDestroy()}
}
