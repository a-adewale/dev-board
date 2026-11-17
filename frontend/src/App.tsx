import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import './App.css'

const apiUrl = 'http://localhost:5080'
type Project = { id: string; name: string; description: string; createdAtUtc: string }
type HealthResponse = { status: string; service: string; checkedAtUtc: string }
type Issue = { id: string; projectId: string; title: string; description: string; status: 'Backlog' | 'Todo' | 'InProgress' | 'Done'; priority: 'Low' | 'Medium' | 'High' | 'Urgent'; createdAtUtc: string; updatedAtUtc: string }
type Comment = { id: string; issueId: string; body: string; author: string; createdAtUtc: string }

function App({ accessToken }: { accessToken?: string }) {
  const [health, setHealth] = useState<HealthResponse | null>(null)
  const [projects, setProjects] = useState<Project[]>([])
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [message, setMessage] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null)
  const [issues, setIssues] = useState<Issue[]>([])
  const [issueTitle, setIssueTitle] = useState('')
  const [selectedIssueId, setSelectedIssueId] = useState<string | null>(null)
  const [comments, setComments] = useState<Comment[]>([])
  const [commentBody, setCommentBody] = useState('')
  const apiFetch = (input: RequestInfo | URL, init?: RequestInit) => fetch(input, { ...init, headers: { ...init?.headers, ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}) } })

  useEffect(() => {
    async function load() {
      try {
        const [healthResponse, projectsResponse] = await Promise.all([apiFetch(`${apiUrl}/api/health`), apiFetch(`${apiUrl}/api/projects`)])
        if (!healthResponse.ok || !projectsResponse.ok) throw new Error('The API could not load DevBoard.')
        setHealth(await healthResponse.json())
        const loadedProjects: Project[] = await projectsResponse.json()
        setProjects(loadedProjects)
        setSelectedProjectId(loadedProjects[0]?.id ?? null)
      } catch (error) { setMessage(error instanceof Error ? error.message : 'The API is unavailable.') }
    }
    void load()
  }, [])

  useEffect(() => {
    if (!selectedProjectId) return
    apiFetch(`${apiUrl}/api/projects/${selectedProjectId}/issues`).then(async (response) => { const result = response.ok ? await response.json() : { items: [] }; setIssues(result.items) }).catch(() => setIssues([]))
  }, [selectedProjectId])

  async function createIssue(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedProjectId || !issueTitle.trim()) return
    const response = await apiFetch(`${apiUrl}/api/projects/${selectedProjectId}/issues`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ title: issueTitle, description: '', status: 'Todo', priority: 'Medium' }) })
    if (response.ok) { const issue: Issue = await response.json(); setIssues((current) => [issue, ...current]); setIssueTitle('') }
  }

  async function moveIssue(issue: Issue, status: Issue['status']) {
    const response = await apiFetch(`${apiUrl}/api/issues/${issue.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ title: issue.title, description: issue.description, status, priority: issue.priority }) })
    if (response.ok) { const updated: Issue = await response.json(); setIssues((current) => current.map((item) => item.id === updated.id ? updated : item)) }
  }

  useEffect(() => {
    if (!selectedIssueId) return
    apiFetch(`${apiUrl}/api/issues/${selectedIssueId}/comments`).then(async (response) => setComments(response.ok ? await response.json() : [])).catch(() => setComments([]))
  }, [selectedIssueId])

  async function addComment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedIssueId || !commentBody.trim()) return
    const response = await apiFetch(`${apiUrl}/api/issues/${selectedIssueId}/comments`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ body: commentBody, author: 'Demo user' }) })
    if (response.ok) { const comment: Comment = await response.json(); setComments((current) => [...current, comment]); setCommentBody('') }
  }

  async function createProject(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setIsSaving(true); setMessage('')
    try {
      const response = await fetch(`${apiUrl}/api/projects${editingId ? `/${editingId}` : ''}`, { method: editingId ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, description }) })
      if (!response.ok) { const problem = await response.json(); throw new Error(problem.message ?? Object.values(problem.errors ?? {}).flat().join(' ')) }
      const project: Project = await response.json()
      setProjects((current) => editingId ? current.map((item) => item.id === project.id ? project : item) : [project, ...current])
      setName(''); setDescription(''); setEditingId(null); setMessage(editingId ? 'Project updated.' : 'Project created.')
    } catch (error) { setMessage(error instanceof Error ? error.message : 'Could not create the project.') }
    finally { setIsSaving(false) }
  }

  return (
    <main className="app-shell">
      <header><p className="eyebrow">DevBoard · Project workspace</p><h1>Make the work visible.</h1><p className="intro">A project tracker built one complete vertical slice at a time.</p></header>
      <section className="layout-grid">
        <form className="panel" onSubmit={createProject}>
          <p className="card-label">{editingId ? 'Edit project' : 'New project'}</p><h2>{editingId ? 'Update workspace' : 'Start a workspace'}</h2>
          <label htmlFor="project-name">Name</label><input id="project-name" value={name} onChange={(event) => setName(event.target.value)} minLength={3} maxLength={100} required placeholder="e.g. Website redesign" />
          <label htmlFor="project-description">Description</label><textarea id="project-description" value={description} onChange={(event) => setDescription(event.target.value)} maxLength={500} rows={4} placeholder="What is this project trying to achieve?" />
          <button type="submit" disabled={isSaving}>{isSaving ? 'Saving…' : editingId ? 'Save changes' : 'Create project'}</button>{editingId && <button type="button" className="button-secondary" onClick={() => { setEditingId(null); setName(''); setDescription('') }}>Cancel edit</button>}{message && <p className="form-message" role="status">{message}</p>}
        </form>
        <section className="panel" aria-labelledby="projects-heading">
          <div className="panel-heading"><div><p className="card-label">Workspace</p><h2 id="projects-heading">Projects</h2></div><span className="count">{projects.length}</span></div>
          {projects.length === 0 ? <p className="empty">No projects yet. Create the first one.</p> : <ul className="project-list">{projects.map((project) => <li key={project.id} className={selectedProjectId === project.id ? 'project-selected' : ''}><button type="button" className="project-select" onClick={() => setSelectedProjectId(project.id)}><strong>{project.name}</strong><p>{project.description || 'No description yet.'}</p></button><button type="button" className="edit-button" onClick={() => { setEditingId(project.id); setName(project.name); setDescription(project.description); setMessage('') }}>Edit</button></li>)}</ul>}
        </section>
      </section>
      {selectedProjectId && <section className="board panel"><div className="panel-heading"><div><p className="card-label">Selected workspace</p><h2>Issue board</h2></div></div><form className="issue-form" onSubmit={createIssue}><input value={issueTitle} onChange={(event) => setIssueTitle(event.target.value)} placeholder="Add an issue…" aria-label="Issue title" /><button type="submit">Add issue</button></form><div className="board-columns">{(['Backlog', 'Todo', 'InProgress', 'Done'] as const).map((status) => <div className="board-column" key={status}><h3>{status === 'InProgress' ? 'In progress' : status}</h3>{issues.filter((issue) => issue.status === status).map((issue) => <article className={`issue-card ${selectedIssueId === issue.id ? 'issue-selected' : ''}`} key={issue.id} onClick={() => setSelectedIssueId(issue.id)}><strong>{issue.title}</strong><small>{issue.priority} priority</small>{status !== 'Done' && <button type="button" onClick={(event) => { event.stopPropagation(); void moveIssue(issue, status === 'Backlog' ? 'Todo' : status === 'Todo' ? 'InProgress' : 'Done') }}>{status === 'Backlog' ? 'Start' : status === 'Todo' ? 'Begin' : 'Complete'}</button>}</article>)}</div>)}</div>{selectedIssueId && <div className="comments"><h3>Discussion</h3><ul>{comments.map((comment) => <li key={comment.id}><strong>{comment.author}</strong><span>{comment.body}</span></li>)}</ul><form className="issue-form" onSubmit={addComment}><input value={commentBody} onChange={(event) => setCommentBody(event.target.value)} placeholder="Write a comment…" aria-label="Comment" /><button type="submit">Comment</button></form></div>}</section>}
      <footer className={health ? 'api-footer api-footer--ok' : 'api-footer'}><span aria-hidden="true">●</span>{health ? `${health.service} is ${health.status}` : 'Connecting to API…'}</footer>
    </main>
  )
}

export default App
