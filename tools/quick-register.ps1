$body = @{
  email       = 'test@mail.com'
  password    = 'test'
  displayName = 'Test User'
} | ConvertTo-Json

try {
  Invoke-RestMethod -Method Post http://localhost:5162/auth/register `
    -ContentType application/json -Body $body
  "Registered OK."
} catch {
  "Register error: $($_.Exception.Message)"
}
