$log = 'C:\Users\alexlocal\projects\Nagomi\tools\rabbit-consume\firewall-result.txt'
try {
    if (-not (Get-NetFirewallRule -DisplayName 'Nagomi RabbitMQ Consume Client' -ErrorAction SilentlyContinue)) {
        New-NetFirewallRule -DisplayName 'Nagomi RabbitMQ Consume Client' -Direction Inbound -Protocol TCP -LocalPort 8095 -Action Allow | Out-Null
        'created' | Out-File $log -Encoding ascii
    } else {
        'already-exists' | Out-File $log -Encoding ascii
    }
} catch {
    ('ERROR: ' + $_.Exception.Message) | Out-File $log -Encoding ascii
}
