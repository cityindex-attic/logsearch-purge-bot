#!/usr/bin/env ruby

require 'json'
require 'base64'
require 'tempfile'
require 'pty'

service_name = ARGV[0]
logfile = ARGV[1]
lumberjack_url = "https://s3.amazonaws.com/ci-labs-buildpack-downloads/lumberjack/lumberjack-1f1f44bc60cb7a271f2bd06f3db1ed666c1a9b22.tar.gz"
puts "----> Configuring lumberjack to ship logs from #{logfile} for service named #{service_name}"

def get_service_credentials(service_name)
	vcap_services = JSON.parse(ENV['VCAP_SERVICES'])
	named_services = vcap_services['user-provided'].select { |item| item['name']==service_name }
	named_services[0]['credentials'] 
end

def write_to_tmp_file(contents)
	file = Tempfile.new('start_lumberjack_shipper')
  	file.write(contents)
  	file.close
  	File.chmod(0600, file.path)
  	file.path
end

def sh(cmd)
	puts "executing #{cmd}"

	PTY.spawn( cmd ) do |stdout_and_err, stdin, pid| 
		begin
		  stdout_and_err.each do |line| 
		  	print line
		  end
		rescue Errno::EIO
			#ignore - see http://stackoverflow.com/questions/10238298/ruby-on-linux-pty-goes-away-without-eof-raises-errnoeio
		ensure
			Process.wait(pid)
		end
	end
end

puts "----> Downloading lumberjack from #{lumberjack_url}"
sh "curl #{lumberjack_url} | tar -C #{Dir.tmpdir} -zx"
sh "chmod +x #{Dir.tmpdir}/lumberjack"

puts "----> Extracting credentials from ENV['VCAP_SERVICES']"

credentials = get_service_credentials('logsearch-ppe-lumberjack_endpoint')
network_servers = credentials["network-servers"]
ssl_ca_path = write_to_tmp_file(Base64.decode64(credentials["network-ssl_ca"]))

puts "----> Generating lumberjack config"
lumberjack_config = <<-eos
{
  "network": {
    "servers": [ "#{network_servers}" ],
    "ssl ca": "#{ssl_ca_path}",
    "timeout": 15
  },
  "files": [
    {
      "paths": [ 
        "#{logfile}"
      ],
      "fields": { "service": "#{service_name}" }
    }
  ]
}
eos
lumberjack_config_path = write_to_tmp_file(lumberjack_config)
puts "#{lumberjack_config_path}\n#{lumberjack_config}"

puts "----> Starting lumberjack shipper..."
sh "#{Dir.tmpdir}/lumberjack -config=#{lumberjack_config_path}"

