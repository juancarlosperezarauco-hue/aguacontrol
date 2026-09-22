"""End-to-end tests against an isolated SQL Server database. Never run against production.
Usage: python integration.py http://127.0.0.1:5081 --password-file ../.local/acceso-inicial.txt
"""
import argparse,base64,datetime,http.cookiejar,json,secrets,urllib.request,urllib.error
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('url');p.add_argument('--password-file',required=True);args=p.parse_args()
password=Path(args.password_file).read_text(encoding='utf-8-sig').splitlines()[1].split(': ',1)[1]
checks=[]
class Session:
 def __init__(self):
  self.op=urllib.request.build_opener(urllib.request.ProxyHandler({}),urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()));self.token=None
 def request(self,path,data=None,method=None,expected=200,csrf=True,raw=None,content_type=None):
  method=method or ('POST' if data is not None else 'GET');headers={}
  if method!='GET' and csrf:headers['X-CSRF-TOKEN']=self.token or ''
  if data is not None:raw=json.dumps(data).encode();content_type='application/json'
  if content_type:headers['Content-Type']=content_type
  req=urllib.request.Request(args.url+'/api'+path,data=raw,method=method,headers=headers)
  try:
   with self.op.open(req,timeout=30) as r:code=r.status;body=r.read();ct=r.headers.get('Content-Type','')
  except urllib.error.HTTPError as e:code=e.code;body=e.read();ct=e.headers.get('Content-Type','')
  assert code==expected,(method,path,expected,code,body[:700])
  return json.loads(body) if body and 'json' in ct else body
 def login(self,login,password):
  self.token=self.request('/auth/csrf')['token'];self.request('/auth/login',{'login':login,'password':password});self.token=self.request('/auth/csrf')['token']
 def create(self,key,data):return self.request('/catalog/'+key,data)
def check(name,condition=True):assert condition,name;checks.append(name);print('PASS',name,flush=True)
admin=Session();admin.login('aquadmin',password)
check('Authentication and database connection',admin.request('/auth/me')['id']>0)
admin.request('/catalog/clients',{'name':'CSRF must fail'},expected=400,csrf=False);check('CSRF protection')
tag=secrets.token_hex(4);user_password='Test-only-1!'+tag
client=admin.create('clients',{'name':'FICTICIO PRUEBA '+tag,'document':'TEST-'+tag,'email':'','phone':'','address':'Direccion ficticia','active':True})
account=admin.create('accounts',{'number':'TEST-'+tag,'active':True})
connection=admin.create('connections',{'code':'TEST-'+tag,'longitude':-60.965,'latitude':-16.38,'address':'Conexion ficticia'})
tariff=admin.create('tariffs',{'name':'Tarifa prueba '+tag,'currency':'BOB','fixedCharge':10,'unitPrice':2,'start':datetime.datetime.now(datetime.timezone.utc).isoformat(),'active':True})
contract=admin.request('/contracts',{'accountId':account['id'],'clientId':client['id'],'connectionId':connection['id'],'tariffId':tariff['id']})
meter=admin.create('meters',{'serial':'TEST-'+tag,'model':'Medidor ficticio','active':True})
installation=admin.request('/installations',{'connectionId':connection['id'],'meterId':meter['id'],'initial':100,'final':None})
reading=admin.request('/readings',{'installationId':installation['id'],'period':'2026-09','value':105,'estimated':False,'note':''})
due=datetime.datetime.now(datetime.timezone.utc).replace(hour=0,minute=0,second=0,microsecond=0).isoformat()
invoice=admin.request('/invoices',{'contractId':contract['id'],'readingId':reading['id'],'dueAt':due})
check('Client -> account -> contract -> meter -> reading -> invoice',invoice['total']==20)
admin.request('/invoices',{'contractId':contract['id'],'readingId':reading['id'],'dueAt':due},expected=400);check('Duplicate billing rejected')
admin.request('/readings',{'installationId':installation['id'],'period':'2026-10','value':99,'estimated':False,'note':''},expected=400);check('Decreasing meter reading rejected')
roles=admin.request('/users')['roles'];role=lambda name:next(x['id'] for x in roles if x['name']==name)
operator=admin.request('/users',{'login':'op'+tag,'name':'OPERARIO FICTICIO','password':user_password,'roleId':role('OPERARIO'),'clientId':None})
portal_user=admin.request('/users',{'login':'cl'+tag,'name':'CLIENTE FICTICIO','password':user_password,'roleId':role('CLIENTE'),'clientId':client['id']})
op=Session();op.login('op'+tag,user_password);portal=Session();portal.login('cl'+tag,user_password)
portal.request('/catalog/clients',expected=403);portal.request('/users',expected=403);check('Portal cannot enumerate customers or security')
check('Portal sees own invoice',any(x['id']==invoice['id'] for x in portal.request('/invoices')))
admin.create('policies',{'threshold':0,'overdueDays':0,'noticeDays':0,'enabled':True})
notice=admin.request('/notices',{'contractId':contract['id']})
types=admin.request('/lookup')['types'];type_id=lambda code:next(x['id'] for x in types if x['code']==code)
order=admin.request('/orders',{'workTypeId':type_id('CORTE'),'contractId':contract['id'],'connectionId':connection['id'],'supervisorId':admin.request('/auth/me')['id'],'priority':'ALTA','scheduledAt':notice['scheduledAt'],'longitude':-60.965,'latitude':-16.38,'instructions':'CORTE FICTICIO SOLO PRUEBA','noticeId':notice['id']})
admin.request('/orders/'+str(order['id'])+'/assign',{'operatorId':operator['id'],'reason':'Prueba','version':order['version']})
check('Notice -> cut work order -> operator assignment')
intent=portal.request('/payment-intents',{'accountId':account['id'],'amount':5,'method':'QR','key':'test-qr-'+tag})
portal.request('/payments/sandbox-confirm',{'intentId':intent['id'],'eventId':'bad-'+tag,'amount':5,'currency':'BOB'},expected=403)
admin.request('/payments/sandbox-confirm',{'intentId':intent['id'],'eventId':'wrong-'+tag,'amount':6,'currency':'BOB'},expected=400);check('Payment authority and amount validated')
pay=admin.request('/payments/sandbox-confirm',{'intentId':intent['id'],'eventId':'qr-'+tag,'amount':5,'currency':'BOB'})
check('Partial payment keeps cut cause',admin.request('/orders/'+str(order['id']))['order']['status']=='ASIGNADA')
again=admin.request('/payments/sandbox-confirm',{'intentId':intent['id'],'eventId':'qr-'+tag,'amount':5,'currency':'BOB'});check('Duplicate payment idempotency',again['id']==pay['id'])
intent2=portal.request('/payment-intents',{'accountId':account['id'],'amount':15,'method':'TARJETA','key':'test-card-'+tag})
admin.request('/payments/sandbox-confirm',{'intentId':intent2['id'],'eventId':'card-'+tag,'amount':15,'currency':'BOB'})
cancelled=admin.request('/orders/'+str(order['id']))['order'];check('Full payment cancels unexecuted cut atomically',cancelled['status']=='CANCELADA')
check('Invoice debt becomes zero',portal.request('/invoices/'+str(invoice['id']))['balance']==0)
check('Operator notified of cancellation',any('PAGO REGISTRADO' in n['message'] for n in op.request('/notifications')))
admin.request('/orders/'+str(order['id'])+'/transition',{'status':'EN_EJECUCION','reason':'Invalid','version':cancelled['version']},expected=400);check('Cancelled state is terminal')
maintenance=admin.request('/orders',{'workTypeId':type_id('MANTENIMIENTO'),'contractId':contract['id'],'connectionId':connection['id'],'supervisorId':admin.request('/auth/me')['id'],'priority':'NORMAL','scheduledAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'longitude':-60.965,'latitude':-16.38,'instructions':'MANTENIMIENTO FICTICIO','noticeId':None});oid=maintenance['id']
admin.request(f'/orders/{oid}/assign',{'operatorId':operator['id'],'reason':'Prueba','version':maintenance['version']})
def transition(session,state,expected=200):
 o=session.request(f'/orders/{oid}')['order'];session.request(f'/orders/{oid}/transition',{'status':state,'reason':'Prueba automatizada','version':o['version']},expected=expected)
transition(op,'EN_CAMINO');transition(op,'EN_EJECUCION');transition(op,'FINALIZADA',400);check('Required activities block premature completion')
detail=op.request(f'/orders/{oid}')
for activity in detail['activities']:
 op.request(f"/orders/{oid}/activities/{activity['id']}",{'done':True,'result':'Actividad ficticia completada','version':activity['version']})
 if activity['evidenceRequired']:
  boundary='AquaTestBoundary';png=base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=')
  data=(f'--{boundary}\r\nContent-Disposition: form-data; name="activityId"\r\n\r\n{activity["id"]}\r\n--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="evidencia-ficticia.png"\r\nContent-Type: image/png\r\n\r\n').encode()+png+f'\r\n--{boundary}--\r\n'.encode()
  op.request(f'/orders/{oid}/evidence',method='POST',raw=data,content_type='multipart/form-data; boundary='+boundary)
material=admin.create('materials',{'code':'TEST-'+tag,'name':'Material ficticio','unit':'unidad','cost':3,'active':True});op.request(f'/orders/{oid}/materials',{'materialId':material['id'],'quantity':2})
transition(op,'FINALIZADA');transition(op,'VERIFICADA',403);transition(admin,'VERIFICADA');transition(admin,'CERRADA');check('Operator execution -> evidence/materials -> supervisor verification -> closed')
check('Audit populated',len(admin.request('/audit'))>10)
check('Cancelled order remains terminal after payment',admin.request('/orders/'+str(order['id']))['order']['status']=='CANCELADA')
print(json.dumps({'passed':len(checks),'checks':checks},ensure_ascii=False,indent=2))
