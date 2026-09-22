async function commercialImportPage(){
  $('#content').innerHTML=intro('Carga comercial','Valida el padrón completo antes de modificar SQL Server')+
    '<div class="notice warning">Seleccione los siete CSV oficiales. La carga inicial exige que clientes, cuentas y conexiones estén vacíos; no se guarda nada si existe un error.</div>'+
    '<section class="card"><div class="card-body"><form id="commercial-import-form"><label>Archivos CSV<input name="files" type="file" accept=".csv,text/csv" multiple required></label><p class="form-error"></p><div class="form-actions"><button type="button" id="validate-import" class="secondary">Validar archivos</button><button class="primary" type="submit">Importar registros válidos</button></div></form><div id="commercial-import-result"></div></div></section>';
  const form=$('#commercial-import-form'), result=$('#commercial-import-result');
  const data=()=>new FormData(form);
  const show=r=>{result.innerHTML='<div class="notice '+(r.valid?'':'warning')+'"><strong>'+(r.valid?'Validación correcta':'Se encontraron errores')+'</strong><br>Filas detectadas: '+Object.values(r.rows??{}).reduce((a,b)=>a+b,0)+(r.imported?' · Registros creados: '+r.imported:'')+'</div>'+((r.errors??[]).length?'<ul>'+r.errors.map(esc).map(x=>'<li>'+x+'</li>').join('')+'</ul>':'');};
  $('#validate-import').onclick=async()=>{try{show(await api('/commercial-import/validate','POST',data()));}catch(e){result.innerHTML='<div class="error">'+esc(e.message)+'</div>';}};
  form.onsubmit=async e=>{e.preventDefault();try{const r=await api('/commercial-import/execute','POST',data());show(r);if(r.valid)await loadLookup();}catch(err){form.querySelector('.form-error').textContent=err.message;}};
}
