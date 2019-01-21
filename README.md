# MQTT.Chat

MQTT.Chat broker is a fully open source, highly scalable, highly available distributed MQTT messaging broker for IoT,It is based on [MQTTnet](https://github.com/chkr1011/MQTTnet) 

 

1.  TLS two-way communication certification
2. Can be private deployment, high reliability and low cost 
3. Support SQLite, SQL Server, PostgreSQL
4. Supports connected clients with different protocol versions at the same time
5. WebSockets supported 
6. Supported MQTT versions 3.1.1;3.1.0



For more information about the MQTT standard, best practices and examples please visit <https://www.hivemq.com/blog/how-to-get-started-with-mqtt>.


## How to install

### Linux  
 -  mkdir  /var/mqttchat 
 -	cp ./*  /var/mqttchat/
 -	chmod 777 MQTT.Chat  
 -	cp  mqttchat.service   /etc/systemd/system/mqttchat.service
 -	sudo systemctl enable  /etc/systemd/system/mqttchat.service 
 -	sudo systemctl start  mqttchat.service 
 -	sudo journalctl -fu  mqttchat.service 
 -	http://127.0.0.1:5000/Swagger/ 

### Windows  
 - sc create mqttchat binPath= "D:\mqttchat\MQTT.Chat.exe" displayname= "MQTT.Chat"  start= auto
 - 




Demo online:  https://mqtt.chat