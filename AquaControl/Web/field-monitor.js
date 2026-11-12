'use strict';
(() => {
 let activeMap,timer,layer,busy=false,failures=0;
 function stop(){clearInterval(timer);timer=null;if(activeMap&&layer)activeMap.removeLayer(layer);activeMap=null;layer=null;}
 async function refresh(){
  if(busy||map!==activeMap||!document.querySelector('#field-monitor'))return;
  busy=true;const target=activeMap;
  try{
   const data=await api('/field/positions');if(map!==target)return;
   layer.clearLayers();failures=0;
   const rows=data.operators,live=rows.filter(x=>x.isLive);
   $('#field-monitor-status').textContent=`${live.length} con señal reciente · ${rows.length} operarios · actualización cada 10 segundos`;
   $('#field-monitor-list').innerHTML=rows.length?table(rows,[{label:'Operario',render:r=>esc(r.name)},{label:'Perfil',render:r=>esc(r.role)},{label:'Estado',render:r=>'<span class="badge '+(r.isLive?'':'warn')+'">'+esc(r.status.replaceAll('_',' '))+'</span>'},{label:'Último envío',render:r=>r.receivedAt?esc(new Date(r.receivedAt).toLocaleTimeString()):'—'},{label:'Precisión',render:r=>r.accuracy!=null?'±'+Math.round(r.accuracy)+' m':'—'},{label:'',render:r=>Number.isFinite(r.latitude)&&Number.isFinite(r.longitude)?act('Ubicar',()=>{target.setView([r.latitude,r.longitude],17);$('#field-monitor-map-hint').textContent=r.isLive?'Ubicación con señal reciente.':'Última ubicación conocida: no es una posición en vivo.';},'secondary'):''}]):'<div class="empty">Cree cuentas de operario y pídales iniciar jornada desde la APK para verlas aquí.</div>';
   for(const row of rows){
    if(!Number.isFinite(row.latitude)||!Number.isFinite(row.longitude))continue;
    const point=L.circleMarker([row.latitude,row.longitude],{radius:10,color:'#fff',weight:3,fillColor:row.isLive?'#5f3dc4':'#87999c',fillOpacity:1});
    point.bindPopup('<strong>'+esc(row.name)+'</strong><br>'+esc(row.isLive?'Posición reciente':'Última posición · sin señal')+'<br>'+esc(row.receivedAt?new Date(row.receivedAt).toLocaleString():'—')).addTo(layer);
   }
  }catch(error){failures++;if(map===target){$('#field-monitor-status').textContent='Sin actualización: '+error.message;layer.clearLayers();if(failures>=3)$('#field-monitor-map-hint').textContent='Conexión interrumpida. No hay posiciones verificadas en vivo.';}}
  finally{busy=false;}
 }
 new MutationObserver(()=>{
  if(route!=='monitor'||!map||!can('field.monitor')||!document.querySelector('#map')){if(activeMap)stop();return;}
  if(map===activeMap)return;stop();activeMap=map;layer=L.layerGroup().addTo(activeMap);
  const section=document.createElement('section');section.id='field-monitor';section.className='card';
  section.innerHTML='<div class="card-head"><div><h3>Operarios en campo</h3><small id="field-monitor-status">Consultando jornadas…</small></div></div><div class="card-body"><p id="field-monitor-map-hint">Puntos morados: señal reciente. Grises: última ubicación conocida. Solo se comparte ubicación durante la jornada autorizada por el operario.</p><div id="field-monitor-list"></div></div>';
  ($('#content .map-layout')??$('#map').closest('.card')).before(section);refresh();timer=setInterval(()=>{if(map!==activeMap)stop();else refresh();},10000);
 }).observe(document.querySelector('#content'),{childList:true,subtree:true});
})();
