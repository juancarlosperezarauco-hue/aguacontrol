let geoImportTimer;
async function importsPage(){
  const response=await Promise.all([api('/geo-imports'),api('/geo-import/job')]),data=response[0],job=response[1];
  const status=job?'<div class="notice '+(job.status==='ERROR'?'warning':'')+'"><strong>'+esc(job.status)+'</strong> · '+esc(job.stage)+'<br>Capa: '+esc(job.layer||'—')+' · Procesados: '+job.processed+' · Aceptados: '+job.accepted+' · Rechazados: '+job.rejected+(job.error?'<br>'+esc(job.error):'')+'</div>':'<div class="notice">No hay una importación SIG en curso.</div>';
  const action=job?.canCancel?act('Cancelar importación',async()=>{await api('/geo-import/job/'+job.id+'/cancel','POST',{});toast('Cancelación solicitada.');},'danger'):act('Iniciar importación',startSig,'primary');
  $('#content').innerHTML=intro('Migrador SIG','Previsualización, importación transaccional, progreso y bitácora',act('Previsualizar capas',previewSig,'secondary')+action)+status+'<div id="sig-preview"></div>'+card('Capas importadas',table(data.imports,[col('layer','Capa'),col('count','Aceptados'),col('rejected','Rechazados'),{label:'Fecha',render:r=>date(r.createdAt)},col('hash','SHA256')]))+card('Últimas incidencias (máximo 100)',table(data.issues,[col('importId','Importación'),col('ordinal','Fila original'),col('reason','Motivo')]));
  clearInterval(geoImportTimer);if(job?.canCancel)geoImportTimer=setInterval(async()=>{if(route==='imports')await importsPage();},2000);
}
async function previewSig(){
  const target=$('#sig-preview');target.innerHTML='<div class="loading">Validando componentes SHP, DBF, SHX, PRJ y WGS84…</div>';
  try{const data=await api('/geo-import/preview');target.innerHTML=card('Previsualización antes de importar','<div class="notice">Fuente: '+esc(data.source)+' · SRID '+data.srid+'. Esta acción no modifica SQL Server.</div>'+table(data.layers,[col('layer','Capa'),col('file','Archivo'),col('records','Registros'),{label:'Campos',render:r=>esc(r.fields.join(', '))},{label:'Muestra',render:r=>esc(r.sample.map(s=>s.geometry).join(', '))}]));}catch(err){target.innerHTML='<div class="error">'+esc(err.message)+'</div>';}
}
async function startSig(){const job=await api('/geo-import/job','POST',{});toast('Importación iniciada: '+job.id);await importsPage();}
new MutationObserver(()=>{
  const controls=document.querySelector('.map-controls');if(!controls||document.querySelector('#sig-legend'))return;
  const legend=document.createElement('section');legend.id='sig-legend';legend.className='map-legend';
  legend.innerHTML='<h3>Leyenda</h3><p><b style="color:#809f9d">■</b> Manzanas</p><p><b style="color:#baa764">■</b> Lotes</p><p><b style="color:#6f95b0">━</b> Vías</p><p><b style="color:#116d72">●</b> Código Fijo</p><p><b style="color:#088879">●</b> Conexiones</p><p><b style="color:#c38735">●</b> Órdenes de trabajo</p><p><b style="color:#75559c">●</b> Puntos de pago</p><small>Los colores de la vista base no cambian esta simbología operativa.</small>';
  controls.append(legend);
}).observe(document.querySelector('#content'),{childList:true,subtree:true});
