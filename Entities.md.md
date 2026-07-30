
Las entidades quedan definidas y relacionadas de forma simplificada de esta forma:

Entidad cabecera de transporte donde se definen princpalmente 2 tipos distintos bases igual que ya definidos en el producto.

- identificador interno
- identificador publico
- Basico o avanzado
	- Basico 
		- puede ser un traslado de ida o ida y vuelta
		- Fecha de inicio y fecha prevista en destino o fecha de cita *fecha y hora*
		- si tiene vuelta se puede definir hora prevista de recogida en la vuelta
	- Avanzado
		- Repticion en el tiempo
			- de fecha A a fecha B
		- De lunes a domingo
		- Por cada dia tiene lo siguiente 
			- Ida o ida y vuelta 
			- fecha de la cita 
			- fecha de inicio 
			- fecha prevista de la vuelta
- Datos basicos de paciente
	- nombre y apellidos
	- edad
	- identificador unico
	- identificador publico
	- ID nacional *DNI o similar*
	- Tarjeta sanitaria
	- numero seguridad social

- Origen
	- Direccion
	- poblacion
	- provicindia
	- codigo postal
	- observaciones
- Destino
	- Tanto origen como desitno pueden ser lo siguiente
		- Domicilio particular
		- Centro sanitario
			- los centros sanitarios son entidades definidas en otra entidad relacionada ya qu enormalmente las proporciona el estado.
- Datos Maestros
	- Motivo de trslado
		- codigo y descripcion que vienen de tabla maestra 
	- requisitos de traslado
		- Oxigeno
		- silla de ruedas
		- camilla
- Datos generales
	- Observaciones del traslado



Una vez generas una peticion de traslado con la cabecera anterior(es el padre de las entidades) el software debe generar una linea por cada traslado.
Es decir, si un traslado cabecera es de ida y vuelta y basico generara 2 lineas de traslado, uno par ala ida y otro par la vuelta.

Si es un traslado avanzado debe generar lo mismo, en base a restricciones, solo los días seleccionados.

Las entidades tanto padre como hijos deben tener ciertos datos del usuario que las genera.
- codigo del usuario
- fecha de creacion, fecha de modificacione etc.. auditoria, en una tabla asociada a las entidades, no tienen porque estar relacionadas simplemente un estilo eventos sobre entiodad para auditoria
- La entidad padre si debe tener dos datos muy importantes, empresa *se obitene del usuario que la crea* y fecha de creacion. 
- estado, las entidades hijos deben tener un estado global, pendiente, planificado, adjudicado, finalizado y anulado. 
- la cabecera debe tener un estado activo, inactivo, anulado
- campo Cliente o customer, la mayoria de veces sera la misma empresa pero peuden generar traslados para otra empresa. 
- Prescriptor del traslado (relacionado con el usuario)

La entidad usuario

- Nombre de usuario
- id
- id publico o codigo de identificacion de usuario
- empresa
- (por ahora nada mas, ya se extender'a a usuarios en openidict, para primera version beta no hay identidad)
- codigo de prescriptor



Tabla entidad eventos/estados del transporte, no de la cabecera sino de cada linea.
- ejemplo, un traslado puede tener varios estados, en en camino a origen, en origen, en camno a destino, en destino, finalizado, anulado (distinto a la anulacion de transporte, hace mas referencia al estado puro pero si esta anulado aqui es anulado alli, forma parte del negocio) los estados los puede generar un vehiculo o un usuario debe poder registrarse este dato tambien, los estados pueden repertirse pero deben poder unificarse, por ejemplo si un usuario asigna y se realizan 2 estados (sin lelgar a finalizar) y luego vuelven a empezar el traslado porque se asigna a otra ambulancia deben almacenarse todos los estados pero los siguientes son los que deben registrarse como transaccion activa o algo similar, como estados activos y los otros quedan relegados a hsitoricos, disenya un sistema adecuado


Una vez estos traslados se generan deben poder planificarse, para MVP muy muy basico, esto requiere
- entidad vehiculo
	- codigo id interno
	- codigo publico de vehiculo (SVA1DES) o cualquier ejemplo estilo esto)
	- (por ahora suficiente)
	- su posicion actual

tabla historico de posiciones del vehiculo



Un panel de cordinacion, me gustaria que fuera algo estilo kanban donde puedes arrastrar traslados a las ambulancias o agrupar por ambulancia ya que esta orientado a empresas pequenyas pero normalmente en gestion es mejor listar los elementos por filtro y asignar vehiculo a ellas, disenyar estas opciones y valorar.
en este panel mostrar paciente, origen, destino y estados, y vehiculo trabajando en el traslado. 

