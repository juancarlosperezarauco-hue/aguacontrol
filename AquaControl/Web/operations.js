async function invoicePage(){
  await loadLookup();const rows=await api('/invoices');
  const create=can('billing.write')?act('＋ Emitir factura',async()=>{const readings=await api('/readings');return form('Emitir factura',[field('contractId','number'),field('readingId','number',false,readings.map(r=>[r.id,`${r.id} · ${r.period} · lectura ${r.value}`])),field('dueAt','datetime-local')],v=>api('/invoices','POST',v));},'primary'):'';
  const bulk=can('billing.write')?act('Generar período',()=>form('Facturación masiva',[field('period'),field('dueAt','datetime-local')],async v=>{const r=await api('/invoices/generate','POST',v);toast(`Facturas emitidas: ${r.emitted}; incidencias: ${r.errors.length}`);return r;},{period:new Date().toISOString().slice(0,7),dueAt:new Date(Date.now()+15*86400000).toISOString()},'Solo se emitirán facturas de contratos vigentes con lectura registrada y sin factura para el período.'),'secondary'):'';
  $('#content').innerHTML=intro('Facturas y deuda','Los saldos incorporan pagos simulados confirmados y ajustes',create+bulk)+card('Documentos de cobro',table(rows,[col('number','Número'),col('holderName','Titular'),col('period','Período'),{label:'Vencimiento',render:r=>date(r.dueAt)},{label:'Total Bs',render:r=>money(r.total)},{label:'Saldo Bs',render:r=>money(r.balance)},{label:'Estado',render:r=>badge(r.balance<=0?'PAGADA':new Date(r.dueAt)<new Date()?'VENCIDA':'PENDIENTE')},{label:'Acciones',render:r=>act('Detalle',()=>invoiceDetail(r.id))+(can('billing.adjust')?act('Ajuste',()=>form('Ajustar factura',[field('amount','number'),field('reason','textarea')],v=>api(`/invoices/${r.id}/adjust`,'POST',v))):'')}]))+act('Exportar CSV',()=>csv('facturas',rows),'secondary');
}
async function noticePage(){
  await loadLookup();const rows=await api('/notices');
  const manual=can('orders.manage')?act('＋ Emitir aviso',()=>form('Emitir aviso de corte',[field('contractId','number')],v=>api('/notices','POST',v),{},'Se aplicará la política de cobranza habilitada.'),'primary'):'';
  const automatic=can('orders.manage')?act('Evaluar toda la cartera',async()=>{const r=await api('/notices/generate','POST');toast(`Avisos creados: ${r.created}; omitidos: ${r.skipped}`);await noticePage();},'secondary'):'';
  $('#content').innerHTML=intro('Avisos de corte','Generación manual o automática según deuda y política',manual+automatic)+card('Avisos',table(rows,[col('id','Aviso'),col('contractId','Contrato'),{label:'Deuda al emitir',render:r=>money(r.debtAtIssue)},{label:'Fecha de corte',render:r=>date(r.scheduledAt)},col('orderId','Orden'),{label:'Estado',render:r=>badge(r.status)}]));
}
new MutationObserver(()=>{
  const location=[...document.querySelectorAll('#content .details div')].find(x=>x.querySelector('small')?.textContent==='Ubicación');
  if(!location||document.querySelector('#route-to-order'))return;
  const match=location.textContent.match(/(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)/);
  if(!match)return;
  const link=document.createElement('a');link.id='route-to-order';link.className='btn secondary';link.target='_blank';link.rel='noopener';
  link.href='https://www.google.com/maps/dir/?api=1&destination='+encodeURIComponent(match[1]+','+match[2]);link.textContent='Abrir ruta al trabajo';
  location.append(document.createElement('br'),link);
}).observe(document.querySelector('#content'),{childList:true,subtree:true});

new MutationObserver(()=>{
  if(!map||map._aquaBaseStyles||!window.L||!document.querySelector('#map'))return;
  map._aquaBaseStyles=true;
  const territorio=L.layerGroup().addTo(map);
  const streets=L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{maxZoom:19,attribution:'© OpenStreetMap contributors'});
  const light=L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png',{maxZoom:20,attribution:'© OpenStreetMap contributors © CARTO'});
  const dark=L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png',{maxZoom:20,attribution:'© OpenStreetMap contributors © CARTO'});
  const satellite=L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',{maxZoom:19,attribution:'Tiles © Esri'});
  L.control.layers({'Territorio SIG':territorio,'Calles':streets,'Vista clara':light,'Vista oscura':dark,'Satélite':satellite},null,{position:'topright',collapsed:false}).addTo(map);
}).observe(document.querySelector('#content'),{childList:true,subtree:true});
