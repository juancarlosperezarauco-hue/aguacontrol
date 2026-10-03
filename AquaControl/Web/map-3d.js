let aqua3dStop;

function aqua3dBounds() {
  const bounds=map.getBounds();
  return `west=${encodeURIComponent(bounds.getWest())}&south=${encodeURIComponent(bounds.getSouth())}&east=${encodeURIComponent(bounds.getEast())}&north=${encodeURIComponent(bounds.getNorth())}`;
}

function aqua3dPolygons(geometry) {
  if (!geometry) return [];
  if (geometry.type==='Polygon') return [geometry.coordinates];
  if (geometry.type==='MultiPolygon') return geometry.coordinates;
  return [];
}

function aqua3dLines(geometry) {
  if (!geometry) return [];
  if (geometry.type==='LineString') return [geometry.coordinates];
  if (geometry.type==='MultiLineString') return geometry.coordinates;
  return [];
}

async function openAqua3d() {
  if (!map || !window.THREE) { toast('El componente de modelo 3D no está disponible. Ejecute scripts/setup.ps1 y recargue la página.',true); return; }
  aqua3dStop?.();
  modal('Modelo 3D del territorio',`<div class="sig-3d"><canvas id="sig-3d-canvas" aria-label="Modelo 3D del territorio"></canvas><section class="sig-3d-panel"><strong>Vista territorial 3D</strong><p>Manzanas, vías y conexiones de la extensión visible. Arrastre para rotar y use la rueda para acercar.</p><p>La extrusión es uniforme y facilita la lectura de las capas; no representa elevación ni altura real de edificios.</p><button id="sig-3d-reset" class="secondary">Restablecer vista</button></section><span id="sig-3d-status" class="sig-3d-status">Cargando capas SIG…</span></div>`);
  const canvas=$('#sig-3d-canvas'),status=$('#sig-3d-status'),dialog=$('#dialog');
  try {
    const query=aqua3dBounds();
    const [blocks,roads]=await Promise.all([api('/geo/blocks?'+query),api('/geo/roads?'+query)]);
    if (!dialog.open || !canvas.isConnected) return;
    const scene=new THREE.Scene();
    scene.background=new THREE.Color('#173842');
    scene.fog=new THREE.Fog('#173842',2000,18000);
    const camera=new THREE.PerspectiveCamera(42,1,1,50000);
    const renderer=new THREE.WebGLRenderer({canvas,antialias:true,alpha:false});
    renderer.setPixelRatio(Math.min(window.devicePixelRatio||1,2));
    renderer.outputColorSpace=THREE.SRGBColorSpace;
    const territory=new THREE.Group(),blocksGroup=new THREE.Group();
    territory.add(blocksGroup); scene.add(territory);
    scene.add(new THREE.HemisphereLight('#d6f1ee','#15343e',2.4));
    const sun=new THREE.DirectionalLight('#fff4cc',2.2); sun.position.set(-3000,-3500,6000); scene.add(sun);
    const center=map.getCenter(), scale=26000*Math.cos(center.lat*Math.PI/180);
    const point=coordinate=>new THREE.Vector2((coordinate[0]-center.lng)*scale,(coordinate[1]-center.lat)*26000);
    const blocksMaterial=new THREE.MeshStandardMaterial({color:'#79aaa4',roughness:.73,metalness:.04,transparent:true,opacity:.92});
    const edgeMaterial=new THREE.LineBasicMaterial({color:'#d5efea',transparent:true,opacity:.45});
    let blockCount=0;
    for (const feature of blocks.features.slice(0,450)) {
      for (const polygon of aqua3dPolygons(feature.geometry)) {
        const outer=polygon[0]||[];
        if (outer.length<3) continue;
        const shape=new THREE.Shape();
        outer.forEach((coordinate,index)=>{const p=point(coordinate);if(index)shape.lineTo(p.x,p.y);else shape.moveTo(p.x,p.y);});
        for (const hole of polygon.slice(1)) {
          if (hole.length<3) continue;
          const path=new THREE.Path();
          hole.forEach((coordinate,index)=>{const p=point(coordinate);if(index)path.lineTo(p.x,p.y);else path.moveTo(p.x,p.y);});
          shape.holes.push(path);
        }
        const geometry=new THREE.ExtrudeGeometry(shape,{depth:28,bevelEnabled:true,bevelSegments:1,bevelSize:1.5,bevelThickness:1.5});
        const mesh=new THREE.Mesh(geometry,blocksMaterial); blocksGroup.add(mesh);
        blocksGroup.add(new THREE.LineSegments(new THREE.EdgesGeometry(geometry),edgeMaterial));
        blockCount++;
      }
    }
    const roadMaterial=new THREE.LineBasicMaterial({color:'#9ec3e2',transparent:true,opacity:.95});
    for (const feature of roads.features.slice(0,700)) {
      for (const line of aqua3dLines(feature.geometry)) {
        if (line.length<2) continue;
        const positions=[];
        line.forEach(coordinate=>{const p=point(coordinate);positions.push(p.x,p.y,25);});
        const geometry=new THREE.BufferGeometry(); geometry.setAttribute('position',new THREE.Float32BufferAttribute(positions,3));
        territory.add(new THREE.Line(geometry,roadMaterial));
      }
    }
    const markerGeometry=new THREE.SphereGeometry(15,12,10),connectionMaterial=new THREE.MeshStandardMaterial({color:'#62e0c7',emissive:'#0d7568',emissiveIntensity:.5});
    let connectionCount=0;
    for (const connection of lookup.connections||[]) {
      if (!Number.isFinite(connection.longitude)||!Number.isFinite(connection.latitude)||!map.getBounds().contains([connection.latitude,connection.longitude])) continue;
      const p=point([connection.longitude,connection.latitude]),marker=new THREE.Mesh(markerGeometry,connectionMaterial); marker.position.set(p.x,p.y,42); territory.add(marker); connectionCount++;
    }
    if (!blockCount) throw Error('No hay manzanas en la extensión visible. Use “Ver territorio” y vuelva a abrir el modelo 3D.');
    const box=new THREE.Box3().setFromObject(blocksGroup),boxCenter=box.getCenter(new THREE.Vector3()),size=box.getSize(new THREE.Vector3()),span=Math.max(size.x,size.y,800);
    territory.position.sub(boxCenter);
    const grid=new THREE.GridHelper(span*2,20,'#47737a','#31545b'); grid.rotation.x=Math.PI/2; grid.position.z=-2; scene.add(grid);
    const reset=()=>{territory.rotation.set(-.82,0,-.18);camera.position.set(0,-span*.78,span*.68);camera.lookAt(0,0,0);};
    reset();
    const resize=()=>{const rect=canvas.getBoundingClientRect();const width=Math.max(1,Math.round(rect.width)),height=Math.max(1,Math.round(rect.height));renderer.setSize(width,height,false);camera.aspect=width/height;camera.updateProjectionMatrix();};
    resize();
    const observer=new ResizeObserver(resize); observer.observe(canvas);
    let frame,dragging=false,lastX=0,lastY=0;
    const draw=()=>{frame=requestAnimationFrame(draw);renderer.render(scene,camera);}; draw();
    const down=event=>{dragging=true;lastX=event.clientX;lastY=event.clientY;canvas.setPointerCapture?.(event.pointerId);};
    const move=event=>{if(!dragging)return;territory.rotation.z+=(event.clientX-lastX)*.006;territory.rotation.x=Math.max(-1.28,Math.min(-.18,territory.rotation.x+(event.clientY-lastY)*.006));lastX=event.clientX;lastY=event.clientY;};
    const up=()=>{dragging=false;};
    const wheel=event=>{event.preventDefault();const factor=event.deltaY>0?1.12:.89;camera.position.multiplyScalar(factor);camera.position.z=Math.max(span*.32,Math.min(span*2.6,camera.position.z));camera.position.y=Math.max(-span*2.6,Math.min(-span*.32,camera.position.y));};
    canvas.addEventListener('pointerdown',down);canvas.addEventListener('pointermove',move);canvas.addEventListener('pointerup',up);canvas.addEventListener('pointercancel',up);canvas.addEventListener('wheel',wheel,{passive:false});
    $('#sig-3d-reset').onclick=reset;
    status.textContent=`${blockCount} manzanas · ${roads.features.length} vías · ${connectionCount} conexiones`;
    aqua3dStop=()=>{cancelAnimationFrame(frame);observer.disconnect();canvas.removeEventListener('pointerdown',down);canvas.removeEventListener('pointermove',move);canvas.removeEventListener('pointerup',up);canvas.removeEventListener('pointercancel',up);canvas.removeEventListener('wheel',wheel);renderer.dispose();aqua3dStop=null;};
    dialog.addEventListener('close',aqua3dStop,{once:true});
  } catch (error) {
    if (canvas.isConnected) { canvas.remove(); status.textContent=error.message||'No se pudo crear el modelo 3D.'; status.classList.add('warning'); }
  }
}

new MutationObserver(()=>{
  const fit=$('#map-fit');
  if (!fit || $('#map-3d')) return;
  const button=document.createElement('button'); button.id='map-3d'; button.type='button'; button.className='secondary'; button.textContent='Modelo 3D'; button.onclick=openAqua3d;
  fit.insertAdjacentElement('afterend',button);
}).observe($('#content'),{childList:true,subtree:true});
