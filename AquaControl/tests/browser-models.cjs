// Deterministic tests for actual UI state models; no browser, SQL data or network requests.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
let queue=new Map(),nextId=0,frames=[],states=[];
const context=vm.createContext({setTimeout:fn=>{queue.set(++nextId,fn);return nextId;},clearTimeout:id=>queue.delete(id),MutationObserver:class{observe(){}},$:()=>null,console});
vm.runInContext(fs.readFileSync(path.join(__dirname,'../Web/history-player.js'),'utf8')+'\nthis.Player=FieldHistoryPlayer;',context);
vm.runInContext(fs.readFileSync(path.join(__dirname,'../Web/map-3d.js'),'utf8'),context);
const player=new context.Player({count:3,onFrame:i=>frames.push(i),onState:s=>states.push(s)});
const tick=()=>{const [id,fn]=queue.entries().next().value??[];assert.ok(fn,'temporizador pendiente');queue.delete(id);fn();};
player.play();player.play();assert.equal(queue.size,1,'Reproducir dos veces no duplica el temporizador');tick();assert.equal(frames.at(-1),1);tick();assert.equal(frames.at(-1),2);assert.equal(player.playing,false,'Se detiene en la última muestra');assert.equal(queue.size,0);
player.play();assert.equal(frames.at(-1),0,'Al reproducir desde el final vuelve al inicio');player.pause();assert.equal(queue.size,0);player.seek(100);assert.equal(player.index,2);player.seek(-10);assert.equal(player.index,0);player.speed(4);assert.equal(player.delay,250);player.play();player.dispose();assert.equal(queue.size,0,'Salir del módulo cancela la reproducción');
const empty=new context.Player({count:0,onFrame:()=>{throw Error('No debe emitir una posición inexistente');}});empty.play();assert.equal(queue.size,0);
const grid=context.aqua3dTileGrid({west:-60.98,east:-60.94,north:-16.36,south:-16.40},17);assert.ok(grid.length>0&&grid.length<=16,'3D limita las teselas descargadas');assert.ok(grid.every(t=>t.west<t.east&&t.south<t.north));assert.ok(grid.some(t=>t.west<=-60.96&&t.east>=-60.96&&t.south<=-16.38&&t.north>=-16.38),'Fondo cartográfico cubre la ubicación original');
console.log('PASS: reproducción, pausa, avance, fin, velocidad, salida y teselas georreferenciadas.');
