// Security rule tests for firestore.rules, run against the Firestore emulator.
// npm test (needs Java). Mirrors the writes made by src/Bookshelf/wwwroot/js/firebase.js.
import { readFileSync } from 'node:fs';
import { after, before, beforeEach, describe, it } from 'node:test';
import {
    assertFails, assertSucceeds, initializeTestEnvironment,
} from '@firebase/rules-unit-testing';
import {
    arrayRemove, arrayUnion, collection, doc, getDoc, getDocs, query, serverTimestamp,
    setDoc, updateDoc, where, deleteDoc, Timestamp, orderBy,
} from 'firebase/firestore';

const CHRIS = { uid: 'chris', email: 'chris@example.com', name: 'Chris' };
const WIFE = { uid: 'wife', email: 'wife@example.com', name: 'Wife' };
const STRANGER = { uid: 'stranger', email: 'stranger@example.com', name: 'Stranger' };
const LIB = 'lib1';

let env;

const as = user => env.authenticatedContext(user.uid, { email: user.email, email_verified: true }).firestore();
const asUnverified = user => env.authenticatedContext(user.uid, { email: user.email, email_verified: false }).firestore();

function newLibrary(user, overrides = {}) {
    return {
        name: 'The Hill Library',
        ownerUid: user.uid,
        memberUids: [user.uid],
        invitedEmails: [],
        members: { [user.uid]: { name: user.name, email: user.email } },
        createdAt: serverTimestamp(),
        ...overrides,
    };
}

function book(id, overrides = {}) {
    return {
        id, title: 'Emma', author: 'Jane Austen', isbn: null, publisher: null, publishedYear: 1815,
        coverUrl: null, notes: null, addedUtc: '2026-10-17T08:00:00+00:00', lastModified: '2026-10-17T08:00:00+00:00',
        isDeleted: false, syncedAt: serverTimestamp(), ...overrides,
    };
}

async function seed(data) {
    await env.withSecurityRulesDisabled(async context => {
        const db = context.firestore();
        await setDoc(doc(db, 'libraries', LIB), data);
    });
}

const seedLibrary = (invited = []) => seed({
    name: 'The Hill Library',
    ownerUid: CHRIS.uid,
    memberUids: [CHRIS.uid],
    invitedEmails: invited,
    members: { [CHRIS.uid]: { name: CHRIS.name, email: CHRIS.email } },
});

before(async () => {
    env = await initializeTestEnvironment({
        projectId: 'demo-bookshelf',
        firestore: { rules: readFileSync(new URL('../../firestore.rules', import.meta.url), 'utf8') },
    });
});

after(() => env.cleanup());

beforeEach(() => env.clearFirestore());

describe('creating a library', () => {
    it('lets a signed-in user create a library with only themselves', async () => {
        await assertSucceeds(setDoc(doc(as(CHRIS), 'libraries', LIB), newLibrary(CHRIS)));
    });

    it('rejects signed-out users', async () => {
        await assertFails(setDoc(doc(env.unauthenticatedContext().firestore(), 'libraries', LIB), newLibrary(CHRIS)));
    });

    it('rejects creating a library owned by someone else', async () => {
        await assertFails(setDoc(doc(as(STRANGER), 'libraries', LIB), newLibrary(CHRIS)));
    });

    it('rejects adding other members or invites at creation', async () => {
        await assertFails(setDoc(doc(as(CHRIS), 'libraries', LIB), newLibrary(CHRIS, { memberUids: [CHRIS.uid, STRANGER.uid] })));
        await assertFails(setDoc(doc(as(CHRIS), 'libraries', LIB), newLibrary(CHRIS, { invitedEmails: ['x@example.com'] })));
    });
});

describe('reading libraries', () => {
    it('members can get and query their libraries', async () => {
        await seedLibrary();
        await assertSucceeds(getDoc(doc(as(CHRIS), 'libraries', LIB)));
        await assertSucceeds(getDocs(query(collection(as(CHRIS), 'libraries'), where('memberUids', 'array-contains', CHRIS.uid))));
    });

    it('invited people can query their invitations', async () => {
        await seedLibrary([WIFE.email]);
        const snapshot = await assertSucceeds(
            getDocs(query(collection(as(WIFE), 'libraries'), where('invitedEmails', 'array-contains', WIFE.email))));
        if (snapshot.size !== 1) throw new Error(`expected 1 invite, got ${snapshot.size}`);
    });

    it('strangers cannot read or look up other people\'s invites', async () => {
        await seedLibrary([WIFE.email]);
        await assertFails(getDoc(doc(as(STRANGER), 'libraries', LIB)));
        await assertFails(getDocs(query(collection(as(STRANGER), 'libraries'), where('invitedEmails', 'array-contains', WIFE.email))));
    });

    it('unverified emails do not count as invited', async () => {
        await seedLibrary([WIFE.email]);
        await assertFails(getDoc(doc(asUnverified(WIFE), 'libraries', LIB)));
    });
});

describe('inviting', () => {
    it('owner can invite and cancel', async () => {
        await seedLibrary();
        const ref = doc(as(CHRIS), 'libraries', LIB);
        await assertSucceeds(updateDoc(ref, { invitedEmails: arrayUnion(WIFE.email) }));
        await assertSucceeds(updateDoc(ref, { invitedEmails: arrayRemove(WIFE.email) }));
    });

    it('owner cannot add members directly or change owner', async () => {
        await seedLibrary();
        const ref = doc(as(CHRIS), 'libraries', LIB);
        await assertFails(updateDoc(ref, { memberUids: arrayUnion(STRANGER.uid) }));
        await assertFails(updateDoc(ref, { ownerUid: STRANGER.uid }));
    });

    it('non-owners cannot invite', async () => {
        await seedLibrary([WIFE.email]);
        await assertFails(updateDoc(doc(as(WIFE), 'libraries', LIB), { invitedEmails: arrayUnion(STRANGER.email) }));
    });
});

describe('accepting an invitation', () => {
    const accept = (db, user) => updateDoc(doc(db, 'libraries', LIB), {
        memberUids: arrayUnion(user.uid),
        invitedEmails: arrayRemove(user.email),
        [`members.${user.uid}`]: { name: user.name, email: user.email },
    });

    it('invited person can join', async () => {
        await seedLibrary([WIFE.email]);
        await assertSucceeds(accept(as(WIFE), WIFE));
        await assertSucceeds(getDoc(doc(as(WIFE), 'libraries', LIB)));
    });

    it('uninvited person cannot join', async () => {
        await seedLibrary([WIFE.email]);
        await assertFails(accept(as(STRANGER), STRANGER));
    });

    it('invited person cannot add someone else or use another invite', async () => {
        await seedLibrary([WIFE.email, STRANGER.email]);
        await assertFails(updateDoc(doc(as(WIFE), 'libraries', LIB), {
            memberUids: arrayUnion(WIFE.uid, STRANGER.uid),
            invitedEmails: arrayRemove(WIFE.email),
            [`members.${WIFE.uid}`]: { name: WIFE.name, email: WIFE.email },
        }));
        await assertFails(updateDoc(doc(as(WIFE), 'libraries', LIB), {
            memberUids: arrayUnion(WIFE.uid),
            invitedEmails: arrayRemove(WIFE.email, STRANGER.email),
            [`members.${WIFE.uid}`]: { name: WIFE.name, email: WIFE.email },
        }));
    });

    it('invited person cannot rename the library', async () => {
        await seedLibrary([WIFE.email]);
        await assertFails(updateDoc(doc(as(WIFE), 'libraries', LIB), { name: 'Mine now' }));
    });
});

describe('books', () => {
    it('members can write and read books with a server timestamp', async () => {
        await seedLibrary();
        const db = as(CHRIS);
        await assertSucceeds(setDoc(doc(db, 'libraries', LIB, 'books', 'b1'), book('b1')));
        await assertSucceeds(getDocs(query(collection(db, 'libraries', LIB, 'books'),
            where('syncedAt', '>', Timestamp.fromMillis(0)), orderBy('syncedAt'))));
    });

    it('new members can read books after joining', async () => {
        await seedLibrary([WIFE.email]);
        await setDoc(doc(as(CHRIS), 'libraries', LIB, 'books', 'b1'), book('b1'));
        await updateDoc(doc(as(WIFE), 'libraries', LIB), {
            memberUids: arrayUnion(WIFE.uid),
            invitedEmails: arrayRemove(WIFE.email),
            [`members.${WIFE.uid}`]: { name: WIFE.name, email: WIFE.email },
        });
        await assertSucceeds(getDoc(doc(as(WIFE), 'libraries', LIB, 'books', 'b1')));
    });

    it('non-members (even invited ones) cannot read or write books', async () => {
        await seedLibrary([WIFE.email]);
        await setDoc(doc(as(CHRIS), 'libraries', LIB, 'books', 'b1'), book('b1'));
        await assertFails(getDoc(doc(as(WIFE), 'libraries', LIB, 'books', 'b1')));
        await assertFails(setDoc(doc(as(STRANGER), 'libraries', LIB, 'books', 'b2'), book('b2')));
    });

    it('rejects client-chosen sync times, mismatched ids and hard deletes', async () => {
        await seedLibrary();
        const db = as(CHRIS);
        await assertFails(setDoc(doc(db, 'libraries', LIB, 'books', 'b1'), book('b1', { syncedAt: Timestamp.fromMillis(1) })));
        await assertFails(setDoc(doc(db, 'libraries', LIB, 'books', 'b1'), book('other')));
        await setDoc(doc(db, 'libraries', LIB, 'books', 'b1'), book('b1'));
        await assertFails(deleteDoc(doc(db, 'libraries', LIB, 'books', 'b1')));
    });
});

describe('readings', () => {
    const joinAsWife = () => updateDoc(doc(as(WIFE), 'libraries', LIB), {
        memberUids: arrayUnion(WIFE.uid),
        invitedEmails: arrayRemove(WIFE.email),
        [`members.${WIFE.uid}`]: { name: WIFE.name, email: WIFE.email },
    });

    function reading(bookId, user, overrides = {}) {
        return {
            id: `${bookId}_${user.uid}`, bookId, uid: user.uid, readerName: user.name,
            rating: 4, review: 'Lovely.', status: 'read', reads: [{ year: 2015, month: 3, day: null }],
            lastModified: '2026-10-17T08:00:00+00:00', syncedAt: serverTimestamp(), ...overrides,
        };
    }

    const ref = (db, bookId, user) => doc(db, 'libraries', LIB, 'readings', `${bookId}_${user.uid}`);

    it('members write their own reading and everyone in the library can read it', async () => {
        await seedLibrary([WIFE.email]);
        await joinAsWife();
        await assertSucceeds(setDoc(ref(as(CHRIS), 'b1', CHRIS), reading('b1', CHRIS)));
        await assertSucceeds(getDoc(ref(as(WIFE), 'b1', CHRIS)));
        await assertSucceeds(getDocs(query(collection(as(WIFE), 'libraries', LIB, 'readings'),
            where('syncedAt', '>', Timestamp.fromMillis(0)), orderBy('syncedAt'))));
    });

    it('allows a reading-list position (whole or fractional)', async () => {
        await seedLibrary();
        await assertSucceeds(setDoc(ref(as(CHRIS), 'b1', CHRIS), reading('b1', CHRIS, { status: 'want', queueOrder: 3 })));
        await assertSucceeds(setDoc(ref(as(CHRIS), 'b1', CHRIS), reading('b1', CHRIS, { status: 'want', queueOrder: 2.5 })));
    });

    it('allows an empty reading (everything cleared)', async () => {
        await seedLibrary();
        await assertSucceeds(setDoc(ref(as(CHRIS), 'b1', CHRIS),
            reading('b1', CHRIS, { rating: null, review: null, status: null, reads: [] })));
    });

    it('nobody can write someone else\'s reading', async () => {
        await seedLibrary([WIFE.email]);
        await joinAsWife();
        await setDoc(ref(as(CHRIS), 'b1', CHRIS), reading('b1', CHRIS));
        // Overwrite Chris's document, or claim to be Chris in a document of their own.
        await assertFails(setDoc(ref(as(WIFE), 'b1', CHRIS), reading('b1', CHRIS, { rating: 1 })));
        await assertFails(setDoc(ref(as(WIFE), 'b1', WIFE), reading('b1', WIFE, { uid: CHRIS.uid })));
        await assertFails(setDoc(ref(as(WIFE), 'b1', WIFE), reading('b1', WIFE, { id: 'b1_other' })));
        await assertFails(setDoc(ref(as(WIFE), 'b1', WIFE), reading('b2', WIFE, { id: 'b1_wife' })));
    });

    it('non-members cannot read or write readings', async () => {
        await seedLibrary([WIFE.email]);
        await setDoc(ref(as(CHRIS), 'b1', CHRIS), reading('b1', CHRIS));
        await assertFails(getDoc(ref(as(STRANGER), 'b1', CHRIS)));
        await assertFails(getDoc(ref(as(WIFE), 'b1', CHRIS)));
        await assertFails(setDoc(ref(as(STRANGER), 'b1', STRANGER), reading('b1', STRANGER)));
    });

    it('rejects bad ratings, statuses, oversized fields and extra keys', async () => {
        await seedLibrary();
        const db = as(CHRIS);
        for (const bad of [
            { rating: 0 }, { rating: 6 }, { rating: 3.5 }, { rating: '5' },
            { status: 'abandoned' },
            { review: 'x'.repeat(10001) },
            { reads: 'often' }, { reads: Array.from({ length: 101 }, () => ({ year: 2000 })) },
            { readerName: 'x'.repeat(201) },
            { queueOrder: 'first' },
            { extra: true },
            { syncedAt: Timestamp.fromMillis(1) },
        ]) {
            await assertFails(setDoc(ref(db, 'b1', CHRIS), reading('b1', CHRIS, bad)));
        }
    });

    it('readings cannot be hard-deleted', async () => {
        await seedLibrary();
        await setDoc(ref(as(CHRIS), 'b1', CHRIS), reading('b1', CHRIS));
        await assertFails(deleteDoc(ref(as(CHRIS), 'b1', CHRIS)));
    });
});
