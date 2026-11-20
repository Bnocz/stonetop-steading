import { expect, test } from 'vitest'
import { mapStateAt } from './route.ts'

const stonetop = { x: 0.69, y: 0.49, label: 'Stonetop' }
const crossroads = { x: 0.56, y: 0.6, label: '' }
const foothills = { x: 0.21, y: 0.24, label: 'Foothills' }
const tower = { x: 0.45, y: 0.8, label: 'The Ruined Tower' }

function session(number: number, waypoints: (typeof stonetop)[]) {
  return { id: `s${number}`, number, title: '', gmOnly: false, waypoints }
}

const xy = ({ x, y }: { x: number; y: number }) => ({ x, y })

test('the first session has no start point and no earlier routes', () => {
  const state = mapStateAt([session(1, [stonetop, crossroads])], 0)

  expect(state.start).toBeNull()
  expect(state.waypoints).toEqual([stonetop, crossroads])
  expect(state.earlier).toEqual([])
  expect(state.party).toEqual(xy(crossroads))
})

test('a later session starts where the party ended the previous one', () => {
  const sessions = [session(1, [stonetop, crossroads]), session(2, [foothills])]

  const state = mapStateAt(sessions, 1)

  expect(state.start).toEqual(xy(crossroads))
  expect(state.waypoints).toEqual([foothills])
  expect(state.earlier).toEqual([[xy(stonetop), xy(crossroads)]])
  expect(state.party).toEqual(xy(foothills))
})

test('earlier routes connect to where each one started', () => {
  const sessions = [
    session(1, [stonetop]),
    session(2, [crossroads]),
    session(3, [foothills]),
    session(4, [tower]),
  ]

  const state = mapStateAt(sessions, 3)

  expect(state.earlier).toEqual([
    [xy(stonetop), xy(crossroads)],
    [xy(crossroads), xy(foothills)],
  ])
  expect(state.start).toEqual(xy(foothills))
})

test('a session without travel keeps the party where it was', () => {
  const sessions = [session(1, [stonetop, crossroads]), session(2, []), session(3, [])]

  const state = mapStateAt(sessions, 2)

  expect(state.start).toEqual(xy(crossroads))
  expect(state.waypoints).toEqual([])
  expect(state.party).toEqual(xy(crossroads))
  expect(state.earlier).toEqual([[xy(stonetop), xy(crossroads)]])
})

test('sessions after the selected one are ignored', () => {
  const sessions = [session(1, [stonetop]), session(2, [tower])]

  const state = mapStateAt(sessions, 0)

  expect(state.party).toEqual(xy(stonetop))
  expect(state.earlier).toEqual([])
})

test('with no waypoints anywhere there is no party marker', () => {
  const state = mapStateAt([session(1, []), session(2, [])], 1)

  expect(state).toEqual({ start: null, waypoints: [], earlier: [], party: null })
})

test('the route being edited can replace the selected session’s waypoints', () => {
  const sessions = [session(1, [stonetop]), session(2, [crossroads])]

  const state = mapStateAt(sessions, 1, [foothills, tower])

  expect(state.start).toEqual(xy(stonetop))
  expect(state.waypoints).toEqual([foothills, tower])
  expect(state.party).toEqual(xy(tower))
})
