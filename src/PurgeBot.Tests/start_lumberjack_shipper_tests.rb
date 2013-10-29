#!/usr/bin/env ruby

def sh_with_cf_env(cmd)
	environment = { "VCAP_SERVICES" => "{\"user-provided\":[{\"name\":\"logsearch-ppe-ssh_tunnel\",\"label\":\"user-provided\",\"tags\":[],\"credentials\":{\"uri\":\"ubuntu@logsearch.example.com\",\"ssh_private_key_base64\":\"dGVzdCBrZXk=\",\"ssh_known_hosts_base64\":\"dGVzdCBrZXk==\"}},{\"name\":\"logsearch-ppe-lumberjack_endpoint\",\"label\":\"user-provided\",\"tags\":[],\"credentials\":{\"network-servers\":\"\\\"logsearch.example.com:5043\\\"\",\"network-ssl_ca\":\"dGVzdCBrZXk=\"}}]}" }
	system(environment, cmd)
end

sh_with_cf_env('../PurgeBot/start_lumberjack_shipper.rb "test-service" "sample.log" ')
