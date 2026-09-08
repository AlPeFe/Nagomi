import { useEffect, useState } from 'react'
import QRCode from 'qrcode'
import { PageHeader } from '../components/States'

/**
 * Pantalla de emparejamiento: muestra un QR que codifica la configuración de la app Android
 * (URL del backend de esta instalación). Desde la app, "Escanear QR de configuración" rellena
 * la URL de la API y la sesión de vehículo. El payload es una URL tipo nagomi://setup?api=...
 */
export function SetupAndroidPage() {
  const [qr, setQr] = useState('')
  const [apiBase, setApiBase] = useState('')

  useEffect(() => {
    // La base real desde la que se sirve esta web (LAN o dominio). En dev con Vite (5173)
    // el backend va en :8080, así que preferimos el origen actual si ya es el de producción.
    const origin = window.location.origin
    const base = origin.includes('localhost:5173') || origin.includes('127.0.0.1:5173')
      ? origin.replace(/:\d+$/, ':8080')
      : origin
    setApiBase(base)

    const payload = `nagomi://setup?api=${encodeURIComponent(base)}`
    QRCode.toDataURL(payload, { width: 280, margin: 1, color: { dark: '#102D3D', light: '#FFFFFF' } })
      .then((url) => setQr(url))
      .catch(() => setQr(''))
  }, [])

  return (
    <div className="page">
      <PageHeader
        eyebrow="App de conductor"
        title="Conectar la app Android"
        description="Escanea este código desde la app Nagomi Driver (Config → Escanear QR) para rellenar automáticamente la URL del backend de esta instalación."
      />
      <div className="card config-card setup-qr-card">
        {qr ? (
          <img src={qr} alt="Código QR de configuración de la app Android" className="setup-qr-img" />
        ) : (
          <p className="muted">Generando código…</p>
        )}
        <dl className="setup-qr-details">
          <div><dt>URL del backend</dt><dd><code>{apiBase}</code></dd></div>
          <div><dt>Formato</dt><dd><code>nagomi://setup?api=…</code></dd></div>
        </dl>
        <p className="muted setup-qr-note">
          La app debe estar en la misma red local que este servidor. Si el móvil no alcanza esta
          URL, abre el puerto en el firewall del host (TCP 8080).
        </p>
      </div>
    </div>
  )
}
