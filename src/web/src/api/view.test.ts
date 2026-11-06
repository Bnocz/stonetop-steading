import { expect, test } from 'vitest'
import { shareTokenFromPath } from './view.ts'

test.each([
  ['/view/abc_DEF-123', 'abc_DEF-123'],
  ['/view/abc123/', 'abc123'],
])('finds the token in %s', (pathname, token) => {
  expect(shareTokenFromPath(pathname)).toBe(token)
})

test.each(['/', '/view', '/view/', '/view/a/b', '/other/abc', '/api/view/abc'])(
  'finds no token in %s',
  (pathname) => {
    expect(shareTokenFromPath(pathname)).toBeNull()
  },
)
