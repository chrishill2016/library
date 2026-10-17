// Thin wrapper over the vendored Firebase SDK (see tools/update-firebase.sh).
// Firestore Lite is used on purpose: the app keeps its own IndexedDB cache.
import { initializeApp } from '../lib/firebase/firebase-app.js';
import {
    connectAuthEmulator, getAuth, GoogleAuthProvider, onAuthStateChanged, signInWithPopup, signOut as authSignOut,
} from '../lib/firebase/firebase-auth.js';
import {
    connectFirestoreEmulator, getFirestore, collection, doc, query, where, orderBy, getDoc, getDocs, addDoc, updateDoc,
    writeBatch, serverTimestamp, arrayUnion, arrayRemove, Timestamp,
} from '../lib/firebase/firebase-firestore-lite.js';

// Firestore allows 500 writes per batch; stay well below so security-rule lookups stay cheap.
const BATCH_SIZE = 100;

let auth;
let db;

export async function initialize(options, dotnetRef) {
    if (auth) return;
    const { emulatorHost, ...config } = options;
    const app = initializeApp(config);
    auth = getAuth(app);
    db = getFirestore(app);
    if (emulatorHost) {
        // Local testing only (see FirebaseOptions.EmulatorHost).
        connectAuthEmulator(auth, `http://${emulatorHost}:9099`, { disableWarnings: true });
        connectFirestoreEmulator(db, emulatorHost, 8080);
    }
    onAuthStateChanged(auth, user => dotnetRef.invokeMethodAsync('OnAuthStateChanged', toUser(user)));
    await auth.authStateReady();
}

function toUser(user) {
    return user
        ? { uid: user.uid, email: user.email?.toLowerCase() ?? null, displayName: user.displayName, photoUrl: user.photoURL }
        : null;
}

export async function signInWithGoogle() {
    const provider = new GoogleAuthProvider();
    provider.setCustomParameters({ prompt: 'select_account' });
    await signInWithPopup(auth, provider);
}

export function signOut() {
    return authSignOut(auth);
}

// ---------- Libraries ----------

function toLibrary(snapshot) {
    const data = snapshot.data();
    return {
        id: snapshot.id,
        name: data.name,
        ownerUid: data.ownerUid,
        memberUids: data.memberUids ?? [],
        invitedEmails: data.invitedEmails ?? [],
        members: data.members ?? {},
    };
}

export async function getMyLibraries(uid) {
    const snapshot = await getDocs(query(collection(db, 'libraries'), where('memberUids', 'array-contains', uid)));
    return snapshot.docs.map(toLibrary);
}

export async function getInvites(email) {
    const snapshot = await getDocs(query(collection(db, 'libraries'), where('invitedEmails', 'array-contains', email.toLowerCase())));
    return snapshot.docs.map(toLibrary);
}

export async function getLibrary(id) {
    const snapshot = await getDoc(doc(db, 'libraries', id));
    return snapshot.exists() ? toLibrary(snapshot) : null;
}

export async function createLibrary(name, user) {
    const ref = await addDoc(collection(db, 'libraries'), {
        name,
        ownerUid: user.uid,
        memberUids: [user.uid],
        invitedEmails: [],
        members: { [user.uid]: { name: user.displayName ?? user.email ?? '', email: user.email ?? '' } },
        createdAt: serverTimestamp(),
    });
    return toLibrary(await getDoc(ref));
}

export function acceptInvite(libraryId, user) {
    return updateDoc(doc(db, 'libraries', libraryId), {
        memberUids: arrayUnion(user.uid),
        invitedEmails: arrayRemove(user.email.toLowerCase()),
        [`members.${user.uid}`]: { name: user.displayName ?? user.email ?? '', email: user.email ?? '' },
    });
}

export function invite(libraryId, email) {
    return updateDoc(doc(db, 'libraries', libraryId), { invitedEmails: arrayUnion(email.toLowerCase()) });
}

export function cancelInvite(libraryId, email) {
    return updateDoc(doc(db, 'libraries', libraryId), { invitedEmails: arrayRemove(email.toLowerCase()) });
}

// ---------- Books ----------

// Returns [{ book, syncedAt }] for books written after sinceMillis (all books when null).
export async function getBookChanges(libraryId, sinceMillis) {
    const books = collection(db, 'libraries', libraryId, 'books');
    const q = sinceMillis == null
        ? query(books, orderBy('syncedAt'))
        : query(books, where('syncedAt', '>', Timestamp.fromMillis(sinceMillis)), orderBy('syncedAt'));
    const snapshot = await getDocs(q);
    return snapshot.docs.map(d => {
        const { syncedAt, ...book } = d.data();
        return { book, syncedAt: syncedAt?.toMillis() ?? 0 };
    });
}

export async function putBooks(libraryId, books) {
    for (let i = 0; i < books.length; i += BATCH_SIZE) {
        const batch = writeBatch(db);
        for (const book of books.slice(i, i + BATCH_SIZE)) {
            batch.set(doc(db, 'libraries', libraryId, 'books', book.id), { ...book, syncedAt: serverTimestamp() });
        }
        await batch.commit();
    }
}
