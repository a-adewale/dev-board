import { StrictMode } from 'react'
import { useEffect, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { Auth0Provider, useAuth0 } from '@auth0/auth0-react'
import './index.css'
import App from './App.tsx'

const domain = import.meta.env.VITE_AUTH0_DOMAIN as string | undefined
const clientId = import.meta.env.VITE_AUTH0_CLIENT_ID as string | undefined
const audience = import.meta.env.VITE_AUTH0_AUDIENCE as string | undefined

function AuthenticatedApp() {
  const { isAuthenticated, isLoading, loginWithRedirect, logout, user, getAccessTokenSilently, error } = useAuth0()
  const [token, setToken] = useState<string>()
  const [tokenError, setTokenError] = useState<string>()
  useEffect(() => {
    if (!isAuthenticated) {
      setToken(undefined)
      setTokenError(undefined)
      return
    }
    void getAccessTokenSilently()
      .then((accessToken) => { setToken(accessToken); setTokenError(undefined) })
      .catch((tokenFailure: Error) => { setToken(undefined); setTokenError(tokenFailure.message) })
  }, [isAuthenticated, getAccessTokenSilently])
  if (isLoading) return <p>Loading authentication…</p>
  return <><div className="auth-bar">{isAuthenticated ? <><span>Signed in as {user?.name ?? user?.email}</span><button type="button" onClick={() => logout({ logoutParams: { returnTo: window.location.origin } })}>Log out</button></> : <button type="button" onClick={() => loginWithRedirect()}>Log in</button>}</div>{error && <p className="auth-error">Auth0 error: {error.message}</p>}{tokenError && <p className="auth-error">API token error: {tokenError}. Check that the API audience is exactly <code>https://devboard-api</code>.</p>}<App accessToken={token} /></>
}

const application = domain && clientId && audience ? <Auth0Provider domain={domain} clientId={clientId} authorizationParams={{ redirect_uri: `${window.location.origin}/callback`, audience }}><AuthenticatedApp /></Auth0Provider> : <App />

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {application}
  </StrictMode>,
)
