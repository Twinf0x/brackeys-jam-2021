using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CorpseSpawner : MonoBehaviour {
    [SerializeField] private GameObject spritePrefab;
    [SerializeField] private Vector3 offset;
    [SerializeField] private AudioClip[] deathSounds;

    public void SpawnCorpse() {
        GameObject spriteObject = Instantiate(spritePrefab);
        spriteObject.transform.position = transform.position + offset;
        spriteObject.GetComponent<Animator>().SetBool("isDead", true);
        spriteObject.GetComponent<AudioSource>()?.PlayOneShot(deathSounds[Random.Range(0, deathSounds.Length)]);
    }
}
