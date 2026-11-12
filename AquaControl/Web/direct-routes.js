'use strict';
let directAssignmentSeed;
function prepareDirectAssignment(code,status){directAssignmentSeed={query:code,kind:status==='CORTADA'?'RECONEXION':status==='PENDIENTE_CORTE'?'CORTE':'LECTURA'};location.hash='assignments';}
async function assignmentsPage(){
 await loadLookup();if(map){map.remove();map=null;}if(!can('orders.manage'))throw Error('Su perfil no puede asignar trabajos.');
 const initial=directAssignmentSeed;directAssignmentSeed=null;
 $('#content').innerHTML=intro('Asignaciones desde el mapa','Seleccione servicios, revise el recorrido y cree las órdenes al guardar.',act('Usar órdenes existentes',existingAssignmentsPage,'secondary'))+
 `<div class="scope-planning"><section class="card"><form id="direct-form" class="card-body scope-form">
 <label for="direct-kind">Trabajo<select id="direct-kind"><option value="CORTE">Corte</option><option value="RECONEXION">Reconexión</option><option value="LECTURA">Lectura de consumo</option></select></label>
 <label for="direct-type">Tipo de trabajo configurado<select id="direct-type" required></select></label>
 <label for="direct-operator">Operario<select id="direct-operator" required></select></label>
 <label for="direct-supervisor">Supervisor<select id="direct-supervisor" required>${(lookup.users??[]).filter(u=>u.supervisor).map(u=>`<option value="${u.id}" ${u.id===me.id?'selected':''}>${esc(u.name)}</option>`).join('')}</select></label>
 <label for="direct-date">Fecha programada<input id="direct-date" type="datetime-local" required value="${new Date(Date.now()-new Date().getTimezoneOffset()*60000).toISOString().slice(0,16)}"></label>
 <label for="direct-period" id="direct-period-label">Período de lectura<input id="direct-period" type="month" value="${new Date().toISOString().slice(0,7)}"></label>
 <label for="direct-mode">Traslado<select id="direct-mode"><option value="VEHICULO">Vehículo</option><option value="MOTO">Moto</option><option value="A_PIE">A pie</option></select></label>
 <label for="direct-instructions">Instrucciones<textarea id="direct-instructions" required maxlength="1500" rows="3">Realizar el trabajo asignado, registrar el medidor y adjuntar evidencia de la visita.</textarea></label>
 <p id="direct-origin" class="notice">Pulse un lugar libre del mapa para marcar la partida.</p><p id="direct-selection" aria-live="polite">Sin servicios seleccionados.</p>
 <button class="primary" type="submit">Calcular y revisar ruta</button><button id="direct-save" type="button" class="secondary" disabled>Crear órdenes y guardar asignación</button><p id="direct-status" role="status"></p></form></section>
 <section class="card scope-map-card"><div id="map" class="scope-map"></div><div class="card-body"><p class="tagline">Puntos verdes: elegibles. Morados: seleccionados. La línea azul se calcula sobre Vías. Marcar un punto no crea órdenes.</p></div></section></div>
 ${card('Buscar servicios elegibles',`<form id="direct-search-form" class="card-body scope-history"><label for="direct-query">Código fijo, cliente o medidor<input id="direct-query" maxlength="100" placeholder="Busque o deje vacío para listar elegibles" value="${esc(initial?.query??'')}"></label><label for="direct-quantity">Cantidad a seleccionar<input id="direct-quantity" type="number" min="1" max="50" value="10"></label><button class="secondary">Buscar servicios</button><button type="button" id="direct-select-first" class="secondary">Seleccionar cantidad de esta página</button><button type="button" id="direct-clear" class="secondary">Limpiar selección</button></form><div class="card-body"><p id="direct-hint">Elija el trabajo y busque los servicios que correspondan.</p><div id="direct-results"></div><button id="direct-next" class="secondary" hidden>Ver siguiente página</button></div>`)}<div id="direct-preview"></div>`;
 const localMap=scopeBaseMap('map'),candidatesLayer=L.layerGroup().addTo(localMap),routeLayer=L.layerGroup().addTo(localMap);
 let origin,originMarker,preview,page=1,rows=[],busy=false,saving=false,requestId=crypto.randomUUID(),searchGeneration=0;const selected=new Map();
 const message=text=>{if(map===localMap)$('#direct-status').textContent=text;};
 const invalidate=()=>{preview=null;$('#direct-save').disabled=true;routeLayer.clearLayers();$('#direct-preview').innerHTML='';requestId=crypto.randomUUID();};
 const draw=()=>{
  candidatesLayer.clearLayers();const visible=new Map([...rows,...selected.values()].map(r=>[r.connectionId,r]));
  for(const row of visible.values()){
   const marker=L.circleMarker([row.latitude,row.longitude],{radius:selected.has(row.connectionId)?10:7,color:'#fff',weight:2,fillColor:selected.has(row.connectionId)?'#6848bb':'#1f9d55',fillOpacity:1}).addTo(candidatesLayer);
   marker.bindTooltip(`${row.client} · ${row.fixedCode??row.connectionCode} · ${row.meter}`);marker.on('click',e=>{L.DomEvent.stopPropagation(e);toggle(row);});
  }
  $('#direct-selection').textContent=`${selected.size} servicio(s) seleccionados · máximo 50 contando los pendientes del operario.`;
  $('#direct-results').innerHTML=table(rows,[{label:'Seleccionar',render:r=>act(selected.has(r.connectionId)?'✓ Seleccionado':'Seleccionar',()=>toggle(r),selected.has(r.connectionId)?'primary':'secondary')},col('client','Cliente'),col('fixedCode','Código fijo'),col('meter','Medidor'),col('connectionCode','Servicio'),{label:'Fecha mínima de corte',render:r=>r.earliestAt?esc(new Date(r.earliestAt).toLocaleString()):'—'}]);
 };
 const toggle=row=>{if(saving)return;if(selected.has(row.connectionId))selected.delete(row.connectionId);else if(selected.size<50)selected.set(row.connectionId,row);else return message('Seleccione como máximo 50 servicios.');invalidate();draw();};
 const configure=()=>{
  searchGeneration++;rows=[];selected.clear();page=1;invalidate();draw();$('#direct-next').hidden=true;
  const kind=$('#direct-kind').value,reader=kind==='LECTURA';$('#direct-period-label').hidden=!reader;
  $('#direct-type').innerHTML=(lookup.types??[]).filter(t=>t.active&&(reader?t.code==='LECTURA':t.effect===kind)).map(t=>`<option value="${t.id}">${esc(t.name)}</option>`).join('');
  $('#direct-operator').innerHTML='<option value="">Seleccionar operario…</option>'+(lookup.users??[]).filter(u=>reader?u.reader:u.technician).map(u=>`<option value="${u.id}">${esc(u.name)}</option>`).join('');
  $('#direct-hint').textContent=kind==='CORTE'?'Requiere aviso vigente, deuda habilitada y medidor instalado. No crea avisos ni habilita cortes por seleccionar puntos.':kind==='RECONEXION'?'Solo servicios cortados con medidor, sin otra reconexión abierta.':'Solo servicios activos con medidor instalado y sin lectura del período ni otra lectura abierta.';
 };
 if(initial)$('#direct-kind').value=initial.kind;configure();
 $('#direct-kind').onchange=configure;$('#direct-period').onchange=configure;
 for(const id of ['direct-type','direct-operator','direct-supervisor','direct-date','direct-mode','direct-instructions'])$('#'+id).oninput=invalidate;
 localMap.on('click',e=>{if(saving)return;origin=e.latlng;invalidate();if(originMarker)originMarker.setLatLng(origin);else originMarker=L.circleMarker(origin,{radius:9,color:'#103f46',fillColor:'#fff',fillOpacity:1,weight:3}).addTo(localMap).bindTooltip('Partida');$('#direct-origin').textContent=`Partida: ${origin.lat.toFixed(6)}, ${origin.lng.toFixed(6)}`;});
 async function searchCandidates(nextPage=1){
  if(busy)return;busy=true;const generation=++searchGeneration;$('#direct-hint').textContent='Buscando servicios elegibles…';
  try{
   const result=await api(`/routes/eligible?kind=${$('#direct-kind').value}&period=${encodeURIComponent($('#direct-period').value)}&query=${encodeURIComponent($('#direct-query').value.trim())}&page=${nextPage}`);
   if(map!==localMap||generation!==searchGeneration)return;page=result.page;rows=result.items;$('#direct-hint').textContent=result.hint+` Página ${page}.`;$('#direct-next').hidden=!result.hasMore;draw();
   if(rows.length)localMap.fitBounds(L.latLngBounds(rows.map(r=>[r.latitude,r.longitude])),{padding:[30,30],maxZoom:17});
  }catch(e){if(map===localMap&&generation===searchGeneration)$('#direct-hint').textContent=e.message;}finally{busy=false;}
 }
 $('#direct-search-form').onsubmit=e=>{e.preventDefault();searchCandidates();};$('#direct-next').onclick=()=>searchCandidates(page+1);
 $('#direct-select-first').onclick=()=>{const quantity=Number($('#direct-quantity').value);if(!Number.isInteger(quantity)||quantity<1||quantity>50)return message('Indique una cantidad entre 1 y 50.');for(const row of rows.slice(0,quantity)){if(selected.size>=50)break;selected.set(row.connectionId,row);}invalidate();draw();};
 $('#direct-clear').onclick=()=>{selected.clear();invalidate();draw();};
 $('#direct-form').onsubmit=async e=>{
  e.preventDefault();if(!origin||!selected.size)return message('Seleccione servicios y marque el punto de partida.');const button=e.submitter;button.disabled=true;invalidate();message('Revalidando servicios y calculando Dijkstra…');
  try{
   const request={requestId,operatorId:Number($('#direct-operator').value),supervisorId:Number($('#direct-supervisor').value),workTypeId:Number($('#direct-type').value),kind:$('#direct-kind').value,connectionIds:[...selected.keys()],scheduledAt:new Date($('#direct-date').value).toISOString(),period:$('#direct-period').value,startLatitude:origin.lat,startLongitude:origin.lng,travelMode:$('#direct-mode').value,instructions:$('#direct-instructions').value};
   const result=await api('/routes/direct-preview','POST',request);if(map!==localMap||requestId!==request.requestId)return;preview=result;
   const path=result.network.path.map(p=>[p[1],p[0]]);if(path.length>1)L.polyline(path,{color:'#335de5',weight:5}).addTo(routeLayer);
   for(const stop of result.network.stops)L.marker([stop.latitude,stop.longitude],{icon:L.divIcon({className:'route-stop',html:String(stop.sequence),iconSize:[28,28],iconAnchor:[14,14]})}).addTo(routeLayer);
   localMap.fitBounds(L.latLngBounds(path.concat(result.network.stops.map(p=>[p.latitude,p.longitude]))),{padding:[35,35],maxZoom:17});
   $('#direct-preview').innerHTML=card('Revise antes de crear las órdenes',`<div class="card-body"><p>${result.orders.filter(o=>o.isNew).length} órdenes nuevas · ${result.orders.filter(o=>!o.isNew).length} pendientes conservadas · ${(result.network.distanceMeters/1000).toFixed(2)} km por vías.</p>${result.network.warnings.map(w=>`<p class="tagline">${esc(w)}</p>`).join('')}</div>`+table(result.orders,[col('sequence','Parada'),col('client','Cliente'),col('connectionCode','Servicio'),col('meter','Medidor'),col('number','Orden'),{label:'Tramo',render:r=>Math.round(r.distanceMeters)+' m'}]));
   $('#direct-save').disabled=false;message('Ruta calculada. Revise las paradas y pulse Crear órdenes y guardar asignación.');
  }catch(error){message(error.message);}finally{button.disabled=false;}
 };
 $('#direct-save').onclick=async()=>{
  if(!preview||saving)return;saving=true;$('#direct-save').disabled=true;const draft=preview;const controls=[...$('#direct-form').querySelectorAll('input,select,textarea,button')];controls.forEach(c=>c.disabled=true);
  try{const route=await api('/routes/direct','POST',{request:draft.request,previewHash:draft.previewHash});if(map!==localMap)return;preview=null;selected.clear();rows=[];draw();$('#direct-preview').innerHTML=card('Asignación guardada',`<div class="card-body"><p>Ruta #${route.id}. Las órdenes, actividades y paradas quedaron guardadas. El operario puede actualizar la APK.</p><a class="btn secondary" href="#orders">Ver órdenes creadas</a></div>`);message('Asignación guardada correctamente.');}
  catch(error){message(error.message+' Puede reintentar el guardado; la misma solicitud no crea órdenes duplicadas.');if(map===localMap&&preview===draft)$('#direct-save').disabled=false;}finally{saving=false;controls.forEach(c=>{if(c.isConnected)c.disabled=false;});if(map===localMap)$('#direct-save').disabled=!preview;}
 };
 if(initial)await searchCandidates();
}
