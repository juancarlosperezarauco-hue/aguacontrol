let aqua3dStop;
function aqua3dBounds(){const b=map.getBounds();return `west=${encodeURIComponent(b.getWest())}&south=${encodeURIComponent(b.getSouth())}&east=${encodeURIComponent(b.getEast())}&north=${encodeURIComponent(b.getNorth())}`;}
function aqua3dPolygons(g){return !g?[]:g.type==='Polygon'?[g.coordinates]:g.type==='MultiPolygon'?g.coordinates:[];}
function aqua3dLines(g){return !g?[]:g.type==='LineString'?[g.coordinates]:g.type==='MultiLineString'?g.coordinates:[];}
function aqua3dTileGrid(bounds,requestedZoom,maxTiles=16){
 const latitude=y=>180/Math.PI*Math.atan(Math.sinh(Math.PI*(1-2*y)));
 for(let z=Math.min(16,Math.max(0,Math.floor(requestedZoom)));z>=0;z--){
  const n=2**z,x=lon=>(lon+180)/360*n,y=lat=>{const r=Math.max(-85.05112878,Math.min(85.05112878,lat))*Math.PI/180;return (1-Math.log(Math.tan(r)+1/Math.cos(r))/Math.PI)/2*n;};
  const west=Math.max(0,Math.floor(x(bounds.west))),east=Math.min(n-1,Math.floor(x(bounds.east))),north=Math.max(0,Math.floor(y(bounds.north))),south=Math.min(n-1,Math.floor(y(bounds.south)));
  if((east-west+1)*(south-north+1)>maxTiles)continue;
  const tiles=[];for(let tx=west;tx<=east;tx++)for(let ty=north;ty<=south;ty++)tiles.push({z,x:tx,y:ty,west:tx/n*360-180,east:(tx+1)/n*360-180,north:latitude(ty/n),south:latitude((ty+1)/n)});return tiles;
 }return [];
}
async function openAqua3d(){
 if(!map||!window.THREE)return toast('El componente 3D no está disponible.',true);
 aqua3dStop?.();const sourceMap=map,view=sourceMap.getBounds(),center=sourceMap.getCenter();
 modal('Visor 3D del territorio',`<div class="sig-3d"><canvas id="sig-3d-canvas" aria-label="Visor tridimensional del territorio"></canvas><section class="sig-3d-panel"><strong>Territorio y servicios</strong><p>Arrastre para rotar. Mayús + arrastrar desplaza. Rueda para acercar.</p><label for="sig-3d-base">Fondo cartográfico<select id="sig-3d-base"><option value="none">Sin fondo</option><option value="streets">Calles</option><option value="satellite">Satélite</option><option value="topo">Topográfico</option></select></label><fieldset><legend>Capas visibles</legend><label><input id="sig-3d-blocks" type="checkbox" checked>Manzanas</label><label><input id="sig-3d-roads" type="checkbox" checked>Vías</label><label><input id="sig-3d-services" type="checkbox" checked>Servicios encontrados</label></fieldset><label for="sig-3d-angle">Inclinación<input id="sig-3d-angle" type="range" min="0" max="70" value="45"></label><button id="sig-3d-top" class="secondary">Vista superior</button><button id="sig-3d-reset" class="secondary">Restablecer</button><p>Extrusión de referencia: no representa alturas reales de edificios.</p><div id="sig-3d-detail" aria-live="polite"></div></section><span id="sig-3d-status" class="sig-3d-status" role="status">Cargando geometrías…</span></div>`);
 const canvas=$('#sig-3d-canvas'),status=$('#sig-3d-status'),dialog=$('#dialog');let disposed=false,renderer,scene,frame,observer;
 const textures=new Set(),tileGroup=new THREE.Group(),terrain=new THREE.Group(),content=new THREE.Group(),blocksGroup=new THREE.Group(),roadsGroup=new THREE.Group(),servicesGroup=new THREE.Group();
 let generation=0;
 const disposeObject=object=>object.traverse(o=>{o.geometry?.dispose();const materials=Array.isArray(o.material)?o.material:[o.material];materials.filter(Boolean).forEach(m=>{if(m.map){m.map.dispose();textures.delete(m.map);}m.dispose();});});
 const cleanup=()=>{if(disposed)return;disposed=true;generation++;cancelAnimationFrame(frame);observer?.disconnect();if(scene)disposeObject(scene);textures.forEach(t=>t.dispose());textures.clear();renderer?.dispose();dialog.removeEventListener('close',cleanup);if(aqua3dStop===cleanup)aqua3dStop=null;};
 aqua3dStop=cleanup;dialog.addEventListener('close',cleanup,{once:true});
 try{
  const [blocks,roads]=await Promise.all([api('/geo/blocks?'+aqua3dBounds()),api('/geo/roads?'+aqua3dBounds())]);if(disposed||!canvas.isConnected)return;
  scene=new THREE.Scene();scene.background=new THREE.Color('#173842');
  const camera=new THREE.PerspectiveCamera(42,1,1,100000);renderer=new THREE.WebGLRenderer({canvas,antialias:true});renderer.setPixelRatio(Math.min(devicePixelRatio||1,2));renderer.outputColorSpace=THREE.SRGBColorSpace;
  scene.add(terrain);terrain.add(content);content.add(tileGroup,blocksGroup,roadsGroup,servicesGroup);scene.add(new THREE.HemisphereLight('#eef8f4','#15343e',2));const sun=new THREE.DirectionalLight('#fff4cc',2);sun.position.set(-3000,-3500,6000);scene.add(sun);
  const factor=6371008.8*Math.PI/180,cos=Math.cos(center.lat*Math.PI/180),point=c=>new THREE.Vector2((c[0]-center.lng)*factor*cos,(c[1]-center.lat)*factor);
  let blockCount=0;
  for(const feature of blocks.features.slice(0,450))for(const polygon of aqua3dPolygons(feature.geometry)){
   if((polygon[0]?.length??0)<3)continue;const shape=new THREE.Shape();polygon[0].forEach((c,i)=>{const p=point(c);if(i)shape.lineTo(p.x,p.y);else shape.moveTo(p.x,p.y);});
   for(const ring of polygon.slice(1)){if(ring.length<3)continue;const hole=new THREE.Path();ring.forEach((c,i)=>{const p=point(c);if(i)hole.lineTo(p.x,p.y);else hole.moveTo(p.x,p.y);});shape.holes.push(hole);}
   const geometry=new THREE.ExtrudeGeometry(shape,{depth:12,bevelEnabled:false});blocksGroup.add(new THREE.Mesh(geometry,new THREE.MeshStandardMaterial({color:'#79aaa4',transparent:true,opacity:.5,roughness:.85})));
   blocksGroup.add(new THREE.LineSegments(new THREE.EdgesGeometry(geometry),new THREE.LineBasicMaterial({color:'#193f48',transparent:true,opacity:.65})));blockCount++;
  }
  for(const feature of roads.features)for(const line of aqua3dLines(feature.geometry)){const vertices=[];for(const c of line){const p=point(c);vertices.push(p.x,p.y,14);}if(vertices.length<6)continue;const geometry=new THREE.BufferGeometry();geometry.setAttribute('position',new THREE.Float32BufferAttribute(vertices,3));roadsGroup.add(new THREE.Line(geometry,new THREE.LineBasicMaterial({color:'#578db5'})));}
  const colors={cortado:'#c53832',pendienteCorte:'#e87921',alDia:'#1f9d55',bajaParcial:'#956338',baja:'#202830',instalacionPendiente:'#285ea8',conDeuda:'#c59a17'};
  for(const item of window.aquaVisibleServices??[]){const c=item.connection;if(!Number.isFinite(c.latitude)||!Number.isFinite(c.longitude)||!view.contains([c.latitude,c.longitude]))continue;const p=point([c.longitude,c.latitude]);const marker=new THREE.Mesh(new THREE.SphereGeometry(5,12,10),new THREE.MeshStandardMaterial({color:colors[item.state]??'#62e0c7'}));marker.position.set(p.x,p.y,21);marker.userData.service=item;servicesGroup.add(marker);}
  const low=point([view.getWest(),view.getSouth()]),high=point([view.getEast(),view.getNorth()]),span=Math.max(high.x-low.x,high.y-low.y,300);
  const reset=()=>{terrain.rotation.set(-Math.PI/4,0,-.12);terrain.position.set(0,0,0);camera.position.set(0,0,span*1.45);camera.up.set(0,1,0);camera.lookAt(0,0,0);$('#sig-3d-angle').value='45';};reset();
  const grid=new THREE.GridHelper(span*2,20,'#47737a','#31545b');grid.rotation.x=Math.PI/2;grid.position.z=-2;content.add(grid);
  const summary=`${blockCount} manzanas · ${roads.features.length} vías · ${servicesGroup.children.length} servicios encontrados${blocks.truncated?' · acerque el mapa para cargar más detalle':''}`;
  const services={streets:'World_Street_Map',satellite:'World_Imagery',topo:'World_Topo_Map'};
  async function base(style){
   const id=++generation;for(const child of [...tileGroup.children]){disposeObject(child);tileGroup.remove(child);}grid.visible=style==='none';if(style==='none'){status.textContent=summary+' · sin fondo';return;}
   status.textContent='Cargando fondo cartográfico…';const tiles=aqua3dTileGrid({west:view.getWest(),east:view.getEast(),north:view.getNorth(),south:view.getSouth()},sourceMap.getZoom(),16);let loaded=0,failed=0;
   await Promise.all(tiles.map(tile=>new Promise(resolve=>{
    const loader=new THREE.TextureLoader();loader.setCrossOrigin('anonymous');loader.load(`https://server.arcgisonline.com/ArcGIS/rest/services/${services[style]}/MapServer/tile/${tile.z}/${tile.y}/${tile.x}`,texture=>{
     if(disposed||id!==generation){texture.dispose();resolve();return;}texture.colorSpace=THREE.SRGBColorSpace;textures.add(texture);
     const sw=point([tile.west,tile.south]),ne=point([tile.east,tile.north]),geometry=new THREE.BufferGeometry();geometry.setAttribute('position',new THREE.Float32BufferAttribute([sw.x,sw.y,0,ne.x,sw.y,0,ne.x,ne.y,0,sw.x,ne.y,0],3));geometry.setAttribute('uv',new THREE.Float32BufferAttribute([0,0,1,0,1,1,0,1],2));geometry.setIndex([0,1,2,0,2,3]);geometry.computeVertexNormals();tileGroup.add(new THREE.Mesh(geometry,new THREE.MeshBasicMaterial({map:texture,side:THREE.DoubleSide})));loaded++;resolve();
    },undefined,()=>{failed++;resolve();});
   })));
   if(!disposed&&id===generation){status.textContent=summary+` · Tiles © Esri · ${loaded}/${tiles.length} teselas`+(failed?' · fondo incompleto; cambie de vista o revise Internet':'');if(failed)grid.visible=true;}
  }
  $('#sig-3d-base').onchange=e=>base(e.target.value);$('#sig-3d-base').value='streets';base('streets');
  for(const [id,group] of [['blocks',blocksGroup],['roads',roadsGroup],['services',servicesGroup]])$('#sig-3d-'+id).onchange=e=>group.visible=e.target.checked;
  $('#sig-3d-angle').oninput=e=>terrain.rotation.x=-Number(e.target.value)*Math.PI/180;$('#sig-3d-top').onclick=()=>{terrain.rotation.set(0,0,0);$('#sig-3d-angle').value='0';};$('#sig-3d-reset').onclick=reset;
  observer=new ResizeObserver(()=>{const r=canvas.getBoundingClientRect();renderer.setSize(Math.max(1,r.width),Math.max(1,r.height),false);camera.aspect=r.width/Math.max(1,r.height);camera.updateProjectionMatrix();});observer.observe(canvas);
  let drag,travel=0;canvas.onpointerdown=e=>{drag={x:e.clientX,y:e.clientY};travel=0;canvas.setPointerCapture(e.pointerId);};canvas.onpointermove=e=>{if(!drag)return;const dx=e.clientX-drag.x,dy=e.clientY-drag.y;travel+=Math.abs(dx)+Math.abs(dy);if(e.shiftKey){terrain.position.x+=dx*span/canvas.clientWidth;terrain.position.y-=dy*span/canvas.clientHeight;}else{terrain.rotation.z+=dx*.006;terrain.rotation.x=Math.max(-1.22,Math.min(0,terrain.rotation.x+dy*.006));$('#sig-3d-angle').value=Math.round(-terrain.rotation.x*180/Math.PI);}drag={x:e.clientX,y:e.clientY};};
  canvas.onpointerup=e=>{
   drag=null;if(travel>5||!servicesGroup.visible)return;const rect=canvas.getBoundingClientRect(),ray=new THREE.Raycaster();ray.setFromCamera(new THREE.Vector2((e.clientX-rect.left)/rect.width*2-1,-(e.clientY-rect.top)/rect.height*2+1),camera);const service=ray.intersectObjects(servicesGroup.children)[0]?.object.userData.service;if(!service)return;
   $('#sig-3d-detail').innerHTML=`<strong>${esc(service.client.name)}</strong><p>${esc(service.connection.code)} · ${esc(service.connection.status)}<br>Medidor: ${esc(service.meter?.serial??'Pendiente de instalación')}</p>`;
  };canvas.onpointercancel=()=>drag=null;canvas.addEventListener('wheel',e=>{e.preventDefault();camera.position.z=Math.max(span*.2,Math.min(span*4,camera.position.z*(e.deltaY>0?1.12:.89)));},{passive:false});
  const draw=()=>{if(disposed)return;frame=requestAnimationFrame(draw);renderer.render(scene,camera);};draw();
 }catch(error){cleanup();if(canvas.isConnected){canvas.remove();status.textContent=error.message||'No se pudo cargar el visor 3D.';}}
}
new MutationObserver(()=>{const fit=$('#map-fit');if(!fit||$('#map-3d'))return;const button=document.createElement('button');button.id='map-3d';button.type='button';button.className='secondary';button.textContent='Visor 3D';button.onclick=openAqua3d;fit.insertAdjacentElement('afterend',button);}).observe($('#content'),{childList:true,subtree:true});
